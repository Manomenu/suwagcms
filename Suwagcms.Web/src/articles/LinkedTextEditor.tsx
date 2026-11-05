import { EditorContent, useEditor } from "@tiptap/react";
import { useEffect, useState } from "react";

import { Button } from "@/design/button";
import { Field } from "@/design/field";

import "./articleEditor.css";
import { extensionsOf } from "./articleEditor";
import { openPicker, type LinkTarget } from "./articleLink";

// Why: it reports Markdown only when the author changes the text, so a text that was opened and not touched is never rewritten.
export function LinkedTextEditor({
    label,
    value,
    targets,
    selfId,
    hint,
    errors,
    onChange,
    onOpen,
}: {
    label: string;
    hint: string;
    errors: string[];
    value: string;
    targets: LinkTarget[];
    selfId: string;
    onChange: (markdown: string) => void;
    onOpen: (id: string, kind: "article" | "procedure") => void;
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
            onOpen: (target, kind) => {
                live.onOpen(target, kind);
            },
        }),
    );
    const invalid = errors.length > 0;
    const editor = useEditor({
        extensions,
        content: value,
        contentType: "markdown",
        immediatelyRender: false,
        editorProps: {
            attributes: {
                id: `article-${label}`,
                role: "textbox",
                "aria-multiline": "true",
                "aria-label": label,
                ...(invalid ? { "aria-invalid": "true" } : {}),
                ...(invalid ? { "aria-describedby": `article-${label}-mistakes` } : {}),
                class: "article-editor h-48 w-full overflow-y-auto rounded-md border border-input bg-secondary p-3 text-sm text-foreground outline-none focus-visible:border-primary aria-invalid:border-destructive",
            },
        },
        onUpdate: ({ editor: changed }) => {
            live.onChange(changed.getMarkdown());
        },
    });

    return (
        <div data-markdown-editor={label}>
            <Field
                prefix="article"
                name={label}
                label={label}
                hint={hint}
                errors={errors}
                action={
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
                }
            >
                {() => (
                    <div data-invalid={invalid}>
                        <EditorContent editor={editor} />
                    </div>
                )}
            </Field>
        </div>
    );
}
