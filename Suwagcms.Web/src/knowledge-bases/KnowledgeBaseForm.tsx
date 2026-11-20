import { useSaveForm } from "@/api/saveForm";
import { LANGUAGES } from "@/api/languages";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { FormFooter } from "@/design/formFooter";
import { Paper } from "@/design/page";
import { Select } from "@/design/select";

import { knowledgeBasesApi } from "./api";
import { HINTS, fieldOf, mistakesFor, saveOf, type FieldName, type KnowledgeBaseForm as FormState } from "./knowledgeBaseForm";

export function KnowledgeBaseForm({
    bot,
    initial,
    editing,
    revision,
    onSaved,
    onReload,
    onCancel,
}: {
    bot: string;
    initial: FormState;
    editing: boolean;
    revision: number;
    onSaved: () => void;
    onReload: () => void;
    onCancel: () => void;
}) {
    const saving = useSaveForm({
        initial,
        save: (form: FormState) => knowledgeBasesApi.save(bot, form.id, saveOf(form), revision),
        onSaved,
        onCancel,
    });
    const { form, change, submit } = saving;

    const mistakes = mistakesFor(saving.mistakes, form.id);
    const errorsOf = (field: FieldName) =>
        mistakes.filter((mistake) => fieldOf(mistake) === field).map((mistake) => `${mistake.message} ${mistake.fix}`);
    const loose = mistakes.filter((mistake) => fieldOf(mistake) === null);
    return (
        <Paper>
            <form onSubmit={submit} className="flex max-w-2xl flex-col gap-3">
                <Field prefix="knowledge-base" name="id" label="Id" hint={HINTS.id} errors={errorsOf("id")}>
                    {(props) => (
                        <Input
                            {...props}
                            value={form.id}
                            disabled={editing}
                            placeholder="offer"
                            onChange={(event) => {
                                change({ id: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
                <Field prefix="knowledge-base" name="title" label="Title" hint={HINTS.title} errors={errorsOf("title")}>
                    {(props) => (
                        <Input
                            {...props}
                            value={form.title}
                            placeholder="Our offer"
                            onChange={(event) => {
                                change({ title: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
                <Field prefix="knowledge-base" name="language" label="Language" hint={HINTS.language} errors={errorsOf("language")}>
                    {(props) => (
                        <Select
                            {...props}
                            value={form.language}
                            onChange={(event) => {
                                change({ language: event.currentTarget.value });
                            }}
                        >
                            {LANGUAGES.map((language) => (
                                <option key={language} value={language}>
                                    {language}
                                </option>
                            ))}
                        </Select>
                    )}
                </Field>
                <Field
                    prefix="knowledge-base"
                    name="embedding"
                    label="Embedding profile"
                    hint={HINTS.embedding}
                    errors={errorsOf("embedding")}
                >
                    {(props) => (
                        <Input
                            {...props}
                            value={form.embedding}
                            placeholder="default"
                            onChange={(event) => {
                                change({ embedding: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
                <Field
                    prefix="knowledge-base"
                    name="results"
                    label="Results per search (1–20)"
                    hint={HINTS.results}
                    errors={errorsOf("results")}
                >
                    {(props) => (
                        <Input
                            {...props}
                            inputMode="numeric"
                            value={form.results}
                            onChange={(event) => {
                                change({ results: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
                <FormFooter saving={saving} loose={loose} editing={editing} reloadLabel="Reload the knowledge bases" onReload={onReload} />
            </form>
        </Paper>
    );
}
