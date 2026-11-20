import { useLayoutEffect, useRef, type ComponentProps } from "react";

import type { Mistake } from "@/api/client";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Select } from "@/design/select";
import { Textarea } from "@/design/textarea";

import type { KnowledgeBase } from "./api";
import type { LinkTarget } from "./articleLink";
import { HINTS, fieldOf, type ArticleForm, type FieldName } from "./articleForm";
import { targetsFor } from "./articlePicker";
import { LinkedTextEditor } from "./LinkedTextEditor";

export function ArticleFields({
    form,
    bases,
    mistakes,
    idLocked,
    targets,
    onOpenArticle,
    onChange,
}: {
    form: ArticleForm;
    bases: KnowledgeBase[];
    mistakes: Mistake[];
    idLocked: boolean;
    targets: LinkTarget[];
    onOpenArticle: (id: string, kind: "article" | "procedure") => void;
    onChange: (change: Partial<ArticleForm>) => void;
}) {
    const errorsOf = (field: FieldName) =>
        mistakes.filter((mistake) => fieldOf(mistake, form.id) === field).map((mistake) => `${mistake.message} ${mistake.fix}`);
    const textEditor = (field: "content" | "llmGuide") => (
        <LinkedTextEditor
            label={field}
            hint={HINTS[field]}
            errors={errorsOf(field)}
            value={form[field]}
            targets={targetsFor(field, targets)}
            selfId={form.id}
            onOpen={onOpenArticle}
            onChange={(markdown) => {
                onChange({ [field]: markdown });
            }}
        />
    );
    return (
        <>
            <div className="flex flex-col gap-3">
                {textEditor("content")}
                {textEditor("llmGuide")}
                <Field prefix="article" name="description" label="description" hint={HINTS.description} errors={errorsOf("description")}>
                    {(props) => (
                        <GrowingTextarea
                            {...props}
                            value={form.description}
                            onChange={(event) => {
                                onChange({ description: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
            </div>
            <div className="grid gap-3 md:grid-cols-4">
                <Field prefix="article" name="id" label="Id" hint={HINTS.id} errors={errorsOf("id")}>
                    {(props) => (
                        <Input
                            {...props}
                            value={form.id}
                            disabled={idLocked}
                            placeholder="priceList"
                            onChange={(event) => {
                                onChange({ id: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
                <Field prefix="article" name="title" label="Title" hint={HINTS.title} errors={errorsOf("title")}>
                    {(props) => (
                        <Input
                            {...props}
                            value={form.title}
                            placeholder="Price list"
                            onChange={(event) => {
                                onChange({ title: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
                <Field
                    prefix="article"
                    name="knowledgeBase"
                    label="Knowledge base"
                    hint={HINTS.knowledgeBase}
                    errors={errorsOf("knowledgeBase")}
                >
                    {(props) => (
                        <Select
                            {...props}
                            className="w-full"
                            value={form.knowledgeBase}
                            onChange={(event) => {
                                onChange({ knowledgeBase: event.currentTarget.value });
                            }}
                        >
                            {!bases.some((base) => base.id === form.knowledgeBase) && (
                                <option value={form.knowledgeBase}>{form.knowledgeBase}</option>
                            )}
                            {bases.map((base) => (
                                <option key={base.id} value={base.id}>
                                    {base.title}
                                </option>
                            ))}
                        </Select>
                    )}
                </Field>
                <Field prefix="article" name="language" label="Language" hint={HINTS.language} errors={[]}>
                    {(props) => (
                        <output {...props} className="flex h-9 items-center">
                            {bases.find((base) => base.id === form.knowledgeBase)?.language ?? "—"}
                        </output>
                    )}
                </Field>
            </div>
        </>
    );
}

function GrowingTextarea(props: ComponentProps<"textarea"> & { value: string }) {
    const ref = useRef<HTMLTextAreaElement>(null);
    useLayoutEffect(() => {
        const box = ref.current;
        if (box === null) return;
        box.style.height = "auto";
        box.style.height = `${String(box.scrollHeight + box.offsetHeight - box.clientHeight)}px`;
    }, [props.value]);
    return <Textarea {...props} ref={ref} className="h-auto min-h-48 font-sans" />;
}
