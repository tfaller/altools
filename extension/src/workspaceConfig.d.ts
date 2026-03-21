export interface WorkspaceConfig {
    projectPath: string;
    transformations?: Record<string, RewriterConfig[]>;
}

export interface RewriterCommentTransformVar {
    type: "comment-transform-var";
    tags?: string;
}

export type RewriterConfig = RewriterCommentTransformVar;