import { useSaveForm } from "@/api/saveForm";
import { FormFooter } from "@/design/formFooter";
import { Paper } from "@/design/page";

import { saveFact } from "./api";
import { bodyOf, fieldOf, type FactForm as FactFormState } from "./factForm";
import { FactFields } from "./FactFields";

/**
 * The form that adds or edits a fact. A refusal shows each mistake beside its field and keeps everything typed. `revision`
 * is the latest one the screen knows of the fact being edited (0 when adding), so reloading and saving again is possible.
 */
export function FactForm({
    bot,
    initial,
    editing,
    revision,
    onSaved,
    onReload,
    onCancel,
}: {
    bot: string;
    initial: FactFormState;
    editing: boolean;
    revision: number;
    onSaved: () => void;
    onReload: () => void;
    onCancel: () => void;
}) {
    const saving = useSaveForm(initial, (form: FactFormState) => saveFact(bot, form.name, bodyOf(form), revision), onSaved);
    const { form, change, submit, mistakes } = saving;

    const loose = mistakes.filter((mistake) => fieldOf(mistake) === null);
    return (
        <Paper>
            <form onSubmit={submit} className="flex max-w-2xl flex-col gap-3">
                <FactFields bot={bot} form={form} mistakes={mistakes} nameLocked={editing} onChange={change} />
                <FormFooter
                    saving={saving}
                    loose={loose}
                    editing={editing}
                    reloadLabel="Reload the facts"
                    onReload={onReload}
                    onCancel={onCancel}
                />
            </form>
        </Paper>
    );
}
