import { existsSync } from "node:fs";
import { basename, join, relative } from "node:path";
import { Uri, window, workspace } from "vscode";
import { getAlWorkspaceUri } from "../workspace";
import { API } from "../git";

export async function workspaceOpen(git: API) {
    const alWorkspaceUri = getAlWorkspaceUri();
    if (!alWorkspaceUri) {
        return;
    }

    const repo = git.getRepository(alWorkspaceUri)
    if (!repo) {
        window.showErrorMessage('No Git repository found in the workspace folder.');
        return;
    }

    const alToolsWorkspacePath = alWorkspaceUri.fsPath + 'ALTools';
    if (!existsSync(alToolsWorkspacePath)) {
        window.showErrorMessage(`The AL workspace '${alToolsWorkspacePath}' does not exist.`);
        return;
    }

    const alWorkspaceRepoPath = relative(repo.rootUri.fsPath, alWorkspaceUri.fsPath);

    workspace.updateWorkspaceFolders(
        workspace.workspaceFolders?.length ?? 0, 0,
        {
            uri: Uri.file(join(alToolsWorkspacePath, alWorkspaceRepoPath)),
            name: basename(alToolsWorkspacePath)
        }
    )
}