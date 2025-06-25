import { existsSync } from 'node:fs';
import { join, basename, dirname, relative } from 'node:path';
import * as child_process from 'node:child_process';
import { promisify } from 'node:util';
import { Uri, window, workspace } from 'vscode';
import { API } from '../git';
import { getAlWorkspaceUri } from '../workspace';

const exec = promisify(child_process.exec);

export async function workspaceInit(git: API) {
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
    const alToolsWorkspacePath = join(alWorkspaceParent, alWorkspaceName + 'ALTools');

    if (existsSync(alToolsWorkspacePath)) {
        // TODO: maybe later open the existing workspace
        window.showErrorMessage(`The AL workspace '${alToolsWorkspacePath}' already exists.`);
        return;
    }

    const branch = branchNameForWorkspaceName(alWorkspaceName);

    if ((await repo.getBranches({ remote: false })).some(b => b.name === branch)) {
        window.showErrorMessage(`Can't create workspace because Git branch '${branch}' already exists.`);
        return;
    }

    await exec(`git worktree add --no-checkout -b ${branch} "${alToolsWorkspacePath}"`, { cwd: alWorkspaceParent });

    const alWorkspaceRepoPath = relative(repo.rootUri.fsPath, alWorkspaceUri.fsPath);
    await exec(`git sparse-checkout set "${alWorkspaceRepoPath}"`, { cwd: alToolsWorkspacePath });

    // git repo extension seems to not find the worktree correctly to checkout it
    // do it also by executing git command directly
    await exec(`git checkout`, { cwd: alToolsWorkspacePath });

    // finaly we can open the new workspace
    workspace.updateWorkspaceFolders(
        workspace.workspaceFolders?.length ?? 0, 0,
        {
            uri: Uri.file(join(alToolsWorkspacePath, alWorkspaceRepoPath)),
            name: alWorkspaceName + 'ALTools'
        }
    )
}

function branchNameForWorkspaceName(workspaceName: string): string {
    if (workspaceName.endsWith('ALTools')) {
        workspaceName = workspaceName.slice(0, -7);
    }
    // just keep safe characters for branch names
    return 'altools-' + workspaceName.replace(/[^a-zA-Z0-9\-]/g, '');
}