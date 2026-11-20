import { useSaveForm } from "@/api/saveForm";
import { FormFooter } from "@/design/formFooter";
import { Paper } from "@/design/page";

import { factsApi } from "./api";
import { bodyOf, fieldOf, type FactForm as FactFormState } from "./factForm";
import { FactFields } from "./FactFields";

export function FactForm({
    bot,
    initial,
    editing,
    revision,
    onSaved,
    onReload,
    onCancel,
    onDirtyChange,
}: {
    bot: string;
    initial: FactFormState;
    editing: boolean;
    revision: number;
    onSaved: () => void;
    onReload: () => void;
    onCancel: () => void;
    onDirtyChange: (dirty: boolean) => void;
}) {
    const saving = useSaveForm({
        initial,
        save: (form: FactFormState) => factsApi.save(bot, form.name, bodyOf(form), revision),
        onSaved,
        onCancel,
        onDirtyChange,
    });
    const { form, change, submit, mistakes } = saving;

    const loose = mistakes.filter((mistake) => fieldOf(mistake) === null);
    return (
        <Paper>
            <form onSubmit={submit} className="flex max-w-2xl flex-col gap-3">
                <FactFields bot={bot} form={form} mistakes={mistakes} nameLocked={editing} onChange={change} />
                <FormFooter saving={saving} loose={loose} editing={editing} reloadLabel="Reload the facts" onReload={onReload} />
            </form>
        </Paper>
    );
}
