import * as vscode from 'vscode';
import { spawn } from 'node:child_process';
import { createOutputChannel } from './PooledOutputChannel';

export const execCommand = (name: string, command: string, args?: string[]) => {
    const outputChannel = createOutputChannel(name);
    outputChannel.show();

    const child = spawn(command, args, {
        stdio: 'pipe',
        detached: false,
        windowsHide: true,
        shell: false,
        cwd: vscode.workspace.workspaceFolders?.[0].uri.fsPath,
    })

    child.stdout.on('data', (data) => {
        outputChannel.append(data.toString());
    })

    child.stderr.on('data', (data) => {
        outputChannel.append(data.toString());
    })

    child.on('error', (error) => {
        outputChannel.appendLine(error.toString());
        outputChannel.done();
    })

    child.on('exit', (code, signal) => {
        outputChannel.appendLine(`Child process exited (${code ?? signal})`);
        outputChannel.done();
    })
}