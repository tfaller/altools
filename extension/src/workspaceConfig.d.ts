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

export interface RewriterCommentTransformCond {
    type: "comment-transform-cond";
    tags?: string;
    flags?: Record<string, boolean>;
}

export interface RewriterCommentQuerySync {
    type: "comment-query-sync";
    tags?: string;
}

export interface RewriterComplexReturnTranspiler {
    type: "complex-return-transpiler";
}

export interface RewriterComplexReturnUplifter {
    type: "complex-return-uplifter";
}

export interface RewriterTempBlobUplifter {
    type: "temp-blob-uplifter";
}

export type RewriterConfig = RewriterCommentTransformVar | RewriterCommentTransformParam | RewriterCommentTransformCond | RewriterCommentQuerySync | RewriterComplexReturnTranspiler | RewriterComplexReturnUplifter | RewriterTempBlobUplifter;