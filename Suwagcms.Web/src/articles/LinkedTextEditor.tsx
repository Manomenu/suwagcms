import { EditorContent, useEditor } from "@tiptap/react";
import { useEffect, useState } from "react";

import { Button } from "@/design/button";

import { extensionsOf } from "./articleEditor";
import { openPicker, type LinkTarget } from "./articleLink";

/**
 * A Markdown text with links to other articles as chips. It reports Markdown only when the author changes the text, so a text
 * that was opened and not touched is never rewritten.
 */
export function LinkedTextEditor({
    id,
    label,
    value,
    targets,
    selfId,
    invalid,
    describedBy,
    onChange,
    onOpen,
}: {
    id: string;
    label: string;
    value: string;
    targets: LinkTarget[];
    selfId: string;
    invalid: boolean;
    describedBy: string | undefined;
    onChange: (markdown: string) => void;
    onOpen: (id: string) => void;
}) {
    // Why: the editor is created once, so what it reads while it lives is kept in one object that every render brings up to date.
    const [live] = useState(() => ({ targets, selfId, onChange, onOpen }));
    useEffect(() => {
        Object.assign(live, { targets, selfId, onChange, onOpen });
    }, [live, targets, selfId, onChange, onOpen]);
    const [extensions] = useState(() =>
        extensionsOf({
            targets: () => live.targets,
            selfId: () => live.selfId,
            onOpen: (target) => {
                live.onOpen(target);
            },
        }),
    );
    const editor = useEditor({
        extensions,
        content: value,
        contentType: "markdown",
        immediatelyRender: false,
        editorProps: {
            attributes: {
                id,
                role: "textbox",
                "aria-multiline": "true",
                "aria-label": label,
                ...(invalid ? { "aria-invalid": "true" } : {}),
                ...(describedBy === undefined ? {} : { "aria-describedby": describedBy }),
                class: "prose-sm min-h-48 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
            },
        },
        onUpdate: ({ editor: changed }) => {
            live.onChange(changed.getMarkdown());
        },
    });

    return (
        <div data-markdown-editor={label} data-invalid={invalid} className="flex flex-col gap-1">
            <div>
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => {
                        if (editor) openPicker(editor);
                    }}
                >
                    Insert link
                </Button>
            </div>
            <EditorContent editor={editor} />
        </div>
    );
}
