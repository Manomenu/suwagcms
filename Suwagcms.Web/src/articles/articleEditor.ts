import type { Extensions } from "@tiptap/core";
import { Markdown } from "@tiptap/markdown";
import { Table, TableCell, TableHeader, TableRow } from "@tiptap/extension-table";
import StarterKit from "@tiptap/starter-kit";

import { ArticleLink, type LinkTarget } from "./articleLink";
import { pickerOf } from "./articlePicker";

const CompactTable = Table.extend({
    renderMarkdown(node, helpers, context) {
        const padded = this.parent?.(node, helpers, context) ?? "";
        return padded
            .trim()
            .split("\n")
            .map((line) =>
                line
                    .trim()
                    .replace(/^\||\|$/g, "")
                    .split(/(?<!\\)\|/)
                    .map((cell) => cell.trim().replace(/^(:?)-+(:?)$/, "$1---$2"))
                    .reduce((row, cell) => `${row} ${cell} |`, "|"),
            )
            .join("\n");
    },
});

export interface EditorLinks {
    targets: () => LinkTarget[];
    selfId: () => string;
    onOpen: (id: string, kind: "article" | "procedure") => void;
}

export function extensionsOf(links: EditorLinks): Extensions {
    return [
        StarterKit.configure({ link: { openOnClick: false } }),
        Markdown,
        CompactTable,
        TableRow,
        TableHeader,
        TableCell,
        ArticleLink.configure({
            titleOf: (id) => links.targets().find((target) => target.id === id && target.kind !== "procedure")?.title,
            deleteTriggerWithBackspace: true,
            onOpen: links.onOpen,
            suggestion: pickerOf(links.targets, links.selfId),
        }),
    ];
}
