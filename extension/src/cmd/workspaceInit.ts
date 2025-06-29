import { existsSync } from 'node:fs';
import { join, basename, dirname, relative } from 'node:path';
import * as child_process from 'node:child_process';
import { promisify } from 'node:util';
import { ExtensionContext, Uri, window, workspace } from 'vscode';
import { API } from '../git';
import { cliExecutable, getAlWorkspaceUri } from '../workspace';
import { readFile, symlink, writeFile } from 'node:fs/promises';

const exec = promisify(child_process.exec);

export async function workspaceInit(context: ExtensionContext, git: API) {
    const alWorkspaceUri = getAlWorkspaceUri();
    if (!alWorkspaceUri) {
        return;
    }

    const repo = git.getRepository(alWorkspaceUri)
    if (!repo) {
        window.showErrorMessage('No Git repository found in the workspace folder.');
        return;
    }

    if (repo.rootUri.fsPath === alWorkspaceUri.fsPath) {
        // Technically it would be possible, because git worktrees work even outside the root of the repository.
        // However, the user probably won't expect files be placed outside of a repository root.
        // And it is actually broken in case of remote/dev containers, where files outside
        // the repository root are not synced to/out from the container.
        window.showErrorMessage("The AL workspace can't be in the root of the Git repository. Please move it to a subfolder.");
        return;
    }

    const alWorkspaceName = basename(alWorkspaceUri.fsPath);
    const alWorkspaceParent = dirname(alWorkspaceUri.fsPath);
    const alToolsWorktreePath = join(alWorkspaceParent, alWorkspaceName + 'ALTools');

    if (existsSync(alToolsWorktreePath)) {
        // TODO: maybe later open the existing workspace
        window.showErrorMessage(`The AL workspace '${alToolsWorktreePath}' already exists.`);
        return;
    }

    const branch = branchNameForWorkspaceName(alWorkspaceName);

    if ((await repo.getBranches({ remote: false })).some(b => b.name === branch)) {
        window.showErrorMessage(`Can't create workspace because Git branch '${branch}' already exists.`);
        return;
    }

    await exec(`git worktree add --no-checkout -b ${branch} "${alToolsWorktreePath}"`, { cwd: alWorkspaceParent });

    const alWorkspaceRepoPath = relative(repo.rootUri.fsPath, alWorkspaceUri.fsPath);
    await exec(`git sparse-checkout set "${alWorkspaceRepoPath}"`, { cwd: alToolsWorktreePath });

    // do the sparse checkout
    const alToolsRepo = (await git.openRepository(Uri.file(alToolsWorktreePath)))!;
    await alToolsRepo.checkout(branch);

    // make symbols available in the new workspace
    const alToolsWorkspacePath = join(alToolsWorktreePath, alWorkspaceRepoPath)
    await symlink(join(alWorkspaceUri.fsPath, '.alpackages'), join(alToolsWorkspacePath, '.alpackages'));

    // uplift the code
    await exec(`${cliExecutable(context)} workspace-transformation "${alToolsWorkspacePath}" complex-return-uplifter`);
    await patchAppJson(alToolsWorkspacePath);

    // commit all uplifted files, so that the user does not have to worry about it
    await alToolsRepo.commit(`${alWorkspaceName}ALTools: Uplift workspace`, { all: true })

    // finaly we can open the new workspace
    workspace.updateWorkspaceFolders(
        workspace.workspaceFolders?.length ?? 0, 0,
        {
            uri: Uri.file(alToolsWorkspacePath),
            name: alWorkspaceName + 'ALTools'
        }
    );
}

function branchNameForWorkspaceName(workspaceName: string): string {
    if (workspaceName.endsWith('ALTools')) {
        workspaceName = workspaceName.slice(0, -7);
    }
    // just keep safe characters for branch names
    return 'altools-' + workspaceName.replace(/[^a-zA-Z0-9\-]/g, '');
}

async function patchAppJson(workspacePath: string) {
    const appFilePath = join(workspacePath, 'app.json');

    const fileRaw = await readFile(appFilePath, 'utf8');
    const app = JSON.parse(fileRaw);

    app.name = app.name + 'ALTools';
    app.runtime = "15.1";

    if (app.target === "Internal") {
        app.target = "OnPrem";
    }

    app.suppressWarnings = [
        ...(app.suppressWarnings ?? []),
        "AL0667" // deprecation warnings - could be invalid for transpiled code to older runtime 
    ];

    await writeFile(appFilePath, JSON.stringify(app, null, 4), 'utf8')
}