import type { Mistake } from "@/api/client";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Textarea } from "@/design/textarea";

import type { KnowledgeBase } from "./api";
import { HINTS, fieldOf, type ArticleForm, type FieldName } from "./articleForm";

const SELECT = "h-9 w-full rounded-md border border-input bg-background px-2 text-sm";

/** The fields of the article editor: content, llmGuide and description side by side, each with a tooltip saying who reads it. */
export function ArticleFields({
    form,
    bases,
    mistakes,
    idLocked,
    onChange,
}: {
    form: ArticleForm;
    bases: KnowledgeBase[];
    mistakes: Mistake[];
    idLocked: boolean;
    onChange: (change: Partial<ArticleForm>) => void;
}) {
    const errorsOf = (field: FieldName) =>
        mistakes.filter((mistake) => fieldOf(mistake) === field).map((mistake) => `${mistake.message} ${mistake.fix}`);
    return (
        <>
            <div className="grid gap-3 md:grid-cols-3">
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
                        <select
                            {...props}
                            className={SELECT}
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
                        </select>
                    )}
                </Field>
            </div>
            <div className="grid gap-3 md:grid-cols-3">
                <Field prefix="article" name="content" label="content" hint={HINTS.content} errors={errorsOf("content")}>
                    {(props) => (
                        <Textarea
                            {...props}
                            value={form.content}
                            onChange={(event) => {
                                onChange({ content: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
                <Field prefix="article" name="llmGuide" label="llmGuide" hint={HINTS.llmGuide} errors={errorsOf("llmGuide")}>
                    {(props) => (
                        <Textarea
                            {...props}
                            value={form.llmGuide}
                            onChange={(event) => {
                                onChange({ llmGuide: event.currentTarget.value });
                            }}
                        />
                    )}
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
