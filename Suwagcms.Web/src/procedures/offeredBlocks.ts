import type { DeclarationStatus } from "@/api/declaration";

import { BLOCKS, type BlockName } from "./draft";

export interface OfferedBlocks {
    blocks: readonly BlockName[];
    note: string | null;
    knowledge: boolean;
}

export function offeredBlocks(status: DeclarationStatus): OfferedBlocks {
    const declaration = status.declaration;
    if (declaration === null || status.formatFix !== null) {
        return { blocks: BLOCKS, note: "This bot has declared no blocks, so nothing is being checked against it.", knowledge: true };
    }
    const declared = declaration.blocks ?? [];
    return { blocks: BLOCKS.filter((block) => declared.includes(block)), note: null, knowledge: declaration.knowledge === true };
}
