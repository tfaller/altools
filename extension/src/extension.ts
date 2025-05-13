// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { execCommand } from './exec';

// This method is called when your extension is activated
// Your extension is activated the very first time the command is executed
export function activate(context: vscode.ExtensionContext) {

	const openApiGenerateDisposable = vscode.commands.registerCommand('altools.openApiGenerate', () => {
		execCommand('ALTools', openApiExecutable(context), ['generate', 'altools-openapi-generator.json'])
	});

	context.subscriptions.push(openApiGenerateDisposable);
}

// This method is called when your extension is deactivated
export function deactivate() { }

const openApiExecutable = (context: vscode.ExtensionContext) =>
	context.asAbsolutePath(executable("bin/TFaller.ALTools.OpenApiGenerator"))

const executable = (exe: string) =>
	process.platform === 'win32' ? `${exe}.exe` : exe
