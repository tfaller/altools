import { existsSync } from "node:fs";
import { join } from "node:path";
import { ExtensionContext, window, workspace } from "vscode";

export function getAlWorkspaceUri() {
    if (workspace.workspaceFolders?.length === 1) {
        return workspace.workspaceFolders[0].uri;
    }

    const editor = window.activeTextEditor;
    if (!editor) {
        window.showErrorMessage('No active editor found.');
        return;
    }

    const workspaceFolder = workspace.getWorkspaceFolder(editor.document.uri);
    if (!workspaceFolder) {
        window.showErrorMessage('No workspace folder found for the active document.');
        return;
    }

    if (!existsSync(join(workspaceFolder.uri.fsPath, '/app.json'))) {
        window.showErrorMessage('No AL projects app.json found in the workspace folder.');
        return;
    }

    return workspaceFolder.uri;
}

export const cliExecutable = (context: ExtensionContext) =>
    context.asAbsolutePath(executable("bin/TFaller.ALTools.Cli"))

const executable = (exe: string) =>
    process.platform === 'win32' ? `${exe}.exe` : exe
