import type { DeclarationStatus } from "@/api/declaration";

import { BLOCKS, type BlockName } from "./draft";

export interface OfferedBlocks {
    blocks: readonly BlockName[];
    note: string | null;
}

/** With nothing to go by (no answer, nothing declared, a format the CMS does not read) every block is offered and the creator says that nothing is being checked. */
export function offeredBlocks(status: DeclarationStatus): OfferedBlocks {
    const declaration = status.declaration;
    if (declaration === null || status.formatFix !== null) {
        return { blocks: BLOCKS, note: "This bot has declared no blocks, so nothing is being checked against it." };
    }
    const declared = declaration.blocks ?? [];
    return { blocks: BLOCKS.filter((block) => declared.includes(block)), note: null };
}
