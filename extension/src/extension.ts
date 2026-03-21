// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { existsSync } from "node:fs"
import { readFile } from "node:fs/promises";
import { join } from 'node:path';
import { execCommand } from './exec';
import { workspaceInit } from './cmd/workspaceInit';
import { workspaceOpen } from './cmd/workspaceOpen';
import { GitExtension } from './git';
import { cliExecutable } from './workspace';
import type { WorkspaceConfig } from './workspaceConfig';

// This method is called when your extension is activated
// Your extension is activated the very first time the command is executed
export function activate(context: vscode.ExtensionContext) {
	const gitExtension = vscode.extensions.getExtension<GitExtension>('vscode.git');
	const git = gitExtension?.exports?.getAPI(1)!;

	const openApiGenerateDisposable = vscode.commands.registerCommand('altools.openApiGenerate', () => {
		execCommand('ALTools', cliExecutable(context), ['openapi', 'generate', 'altools-openapi-generator.json'])
	});
	context.subscriptions.push(openApiGenerateDisposable);

	const xmlGenerateDisposable = vscode.commands.registerCommand('altools.xmlGenerate', () => {
		execCommand('ALTools', cliExecutable(context), ['xml', 'generate', 'altools-xml-generator.json'])
	});
	context.subscriptions.push(xmlGenerateDisposable);

	const openWorkspaceDisposable = vscode.commands.registerCommand('altools.workspaceInit', async () => {
		await vscode.window.withProgress({
			location: vscode.ProgressLocation.Notification,
			title: "ALTools workspace initialization",
			cancellable: false
		}, async () => {
			await workspaceInit(context, git);
		})
	})
	context.subscriptions.push(openWorkspaceDisposable);

	const openWorkspaceOpenDisposable = vscode.commands.registerCommand('altools.workspaceOpen', async () => {
		await workspaceOpen(git);
	});
	context.subscriptions.push(openWorkspaceOpenDisposable);

	const transformationRunDisposable = vscode.commands.registerCommand('altools.transformationRun', () => transformationRun(context));
	context.subscriptions.push(transformationRunDisposable);
}

async function transformationRun(context: vscode.ExtensionContext) {
	const workspaceUri = vscode.workspace.workspaceFolders?.[0].uri.fsPath;
	if (!workspaceUri) {
		vscode.window.showErrorMessage('No workspace folder found.');
		return;
	}

	const configPath = join(workspaceUri, 'altools.json');
	if (!existsSync(configPath)) {
		vscode.window.showErrorMessage(`No ALTools configuration file found at ${configPath}`);
		return;
	}

	const configFile = await readFile(configPath, 'utf-8');
	const config: WorkspaceConfig = JSON.parse(configFile);

	const items: vscode.QuickPickItem[] = Object.keys(config.transformations ?? {}).map(key => ({
		label: key,
		description: `Transformation: ${key}`
	}));

	if (items.length === 0) {
		vscode.window.showErrorMessage('No transformations found in the configuration.');
		return;
	}

	const transformation = await vscode.window.showQuickPick(items);
	if (!transformation) {
		return;
	}

	execCommand('ALTools', cliExecutable(context), ['workspace-transformation', '.', 'config', transformation.label]);
}

// This method is called when your extension is deactivated
export function deactivate() { }