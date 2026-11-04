export interface WorkspaceConfig {
    projectPath: string;
    transformations?: Record<string, RewriterConfig[]>;
}

export interface RewriterCommentTransformVar {
    type: "comment-transform-var";
    tags?: string;
}

export interface RewriterCommentTransformParam {
    type: "comment-transform-param";
    tags?: string;
}

export interface RewriterTempBlobUplifter {
    type: "temp-blob-uplifter";
}

export type RewriterConfig = RewriterCommentTransformVar | RewriterCommentTransformParam | RewriterTempBlobUplifter;