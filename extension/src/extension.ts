// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { execCommand } from './exec';
import { workspaceInit } from './cmd/workspaceInit';
import { workspaceOpen } from './cmd/workspaceOpen';
import { GitExtension } from './git';

// This method is called when your extension is activated
// Your extension is activated the very first time the command is executed
export function activate(context: vscode.ExtensionContext) {
	const gitExtension = vscode.extensions.getExtension<GitExtension>('vscode.git');
	const git = gitExtension?.exports?.getAPI(1)!;

	const openApiGenerateDisposable = vscode.commands.registerCommand('altools.openApiGenerate', () => {
		execCommand('ALTools', openApiExecutable(context), ['openapi', 'generate', 'altools-openapi-generator.json'])
	});
	context.subscriptions.push(openApiGenerateDisposable);

	const xmlGenerateDisposable = vscode.commands.registerCommand('altools.xmlGenerate', () => {
		execCommand('ALTools', openApiExecutable(context), ['xml', 'generate', 'altools-xml-generator.json'])
	});
	context.subscriptions.push(xmlGenerateDisposable);

	const openWorkspaceDisposable = vscode.commands.registerCommand('altools.workspaceInit', async () => {
		await workspaceInit(git);
	})
	context.subscriptions.push(openWorkspaceDisposable);

	const openWorkspaceOpenDisposable = vscode.commands.registerCommand('altools.workspaceOpen', async () => {
		await workspaceOpen(git);
	});
	context.subscriptions.push(openWorkspaceOpenDisposable);
}

// This method is called when your extension is deactivated
export function deactivate() { }

const openApiExecutable = (context: vscode.ExtensionContext) =>
	context.asAbsolutePath(executable("bin/TFaller.ALTools.Cli"))

const executable = (exe: string) =>
	process.platform === 'win32' ? `${exe}.exe` : exe
