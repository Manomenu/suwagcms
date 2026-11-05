import type { Mistake } from "@/api/client";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Select } from "@/design/select";
import { Textarea } from "@/design/textarea";

import type { KnowledgeBase } from "./api";
import type { LinkTarget } from "./articleLink";
import { HINTS, fieldOf, type ArticleForm, type FieldName } from "./articleForm";
import { LinkedTextEditor } from "./LinkedTextEditor";

/** The fields of the article editor: content, llmGuide and description side by side, each with a tooltip saying who reads it. */
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
    onOpenArticle: (id: string) => void;
    onChange: (change: Partial<ArticleForm>) => void;
}) {
    const errorsOf = (field: FieldName) =>
        mistakes.filter((mistake) => fieldOf(mistake, form.id) === field).map((mistake) => `${mistake.message} ${mistake.fix}`);
    const textEditor = (field: "content" | "llmGuide") => (
        <LinkedTextEditor
            id={`article-${field}`}
            label={field}
            value={form[field]}
            targets={targets}
            selfId={form.id}
            invalid={errorsOf(field).length > 0}
            describedBy={errorsOf(field).length > 0 ? `article-${field}-mistakes` : undefined}
            onOpen={onOpenArticle}
            onChange={(markdown) => {
                onChange({ [field]: markdown });
            }}
        />
    );
    return (
        <>
            <div className="grid gap-3 md:grid-cols-4">
                <Field prefix="article" name="id" label="Id" errors={errorsOf("id")}>
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
                <Field prefix="article" name="title" label="Title" errors={errorsOf("title")}>
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
                <Field prefix="article" name="knowledgeBase" label="Knowledge base" errors={errorsOf("knowledgeBase")}>
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
            <div className="grid gap-3 md:grid-cols-3">
                <Field prefix="article" name="content" label="content" hint={HINTS.content} errors={errorsOf("content")}>
                    {() => textEditor("content")}
                </Field>
                <Field prefix="article" name="llmGuide" label="llmGuide" hint={HINTS.llmGuide} errors={errorsOf("llmGuide")}>
                    {() => textEditor("llmGuide")}
                </Field>
                <Field prefix="article" name="description" label="description" hint={HINTS.description} errors={errorsOf("description")}>
                    {(props) => (
                        <Textarea
                            {...props}
                            value={form.description}
                            onChange={(event) => {
                                onChange({ description: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
            </div>
        </>
    );
}
