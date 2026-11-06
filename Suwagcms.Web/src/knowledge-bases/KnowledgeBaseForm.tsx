import { useSaveForm } from "@/api/saveForm";
import { LANGUAGES } from "@/api/languages";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { FormFooter } from "@/design/formFooter";
import { Paper } from "@/design/page";
import { Select } from "@/design/select";

import { saveKnowledgeBase } from "./api";
import { HINTS, fieldOf, mistakesFor, type FieldName, type KnowledgeBaseForm as FormState } from "./knowledgeBaseForm";

/**
 * The form that adds a knowledge base or edits one. A refusal shows each mistake beside its field and keeps everything typed.
 * `revision` is the latest one the screen knows of the base being edited (0 when adding).
 */
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
    const saving = useSaveForm(
        initial,
        (form: FormState) => saveKnowledgeBase(bot, form.id, { title: form.title, language: form.language }, revision),
        onSaved,
    );
    const { form, change, submit } = saving;

    const mistakes = mistakesFor(saving.mistakes, form.id);
    const errorsOf = (field: FieldName) =>
        mistakes.filter((mistake) => fieldOf(mistake) === field).map((mistake) => `${mistake.message} ${mistake.fix}`);
    const loose = mistakes.filter((mistake) => fieldOf(mistake) === null);
    return (
        <Paper>
            <form onSubmit={submit} className="flex max-w-2xl flex-col gap-3">
                <Field prefix="knowledge-base" name="id" label="Id" errors={errorsOf("id")}>
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
                <Field prefix="knowledge-base" name="title" label="Title" errors={errorsOf("title")}>
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
                <FormFooter
                    saving={saving}
                    loose={loose}
                    editing={editing}
                    reloadLabel="Reload the knowledge bases"
                    onReload={onReload}
                    onCancel={onCancel}
                />
            </form>
        </Paper>
    );
}
