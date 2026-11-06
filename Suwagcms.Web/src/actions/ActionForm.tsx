import { useSaveForm } from "@/api/saveForm";
import { FormFooter } from "@/design/formFooter";
import { Paper } from "@/design/page";

import { bodyOf, placeOf, placesOf, type ActionForm as ActionFormState } from "./actionForm";
import { ActionFields } from "./ActionFields";
import { saveAction } from "./api";

/** The form that adds or edits an action. A refusal shows each mistake beside its field and keeps everything typed. */
export function ActionForm({
    bot,
    initial,
    editing,
    revision,
    onSaved,
    onReload,
    onCancel,
    testRun,
}: {
    bot: string;
    initial: ActionFormState;
    editing: boolean;
    revision: number;
    onSaved: () => void;
    onReload: () => void;
    onCancel: () => void;
    testRun?: (unsaved: boolean) => React.ReactNode;
}) {
    const saving = useSaveForm(initial, (form: ActionFormState) => saveAction(bot, form.id, bodyOf(form), revision), onSaved);
    const { form, change, submit, mistakes } = saving;

    const places = placesOf(form);
    const loose = mistakes.filter((mistake) => !places.includes(placeOf(mistake)));
    const unsaved = JSON.stringify(form) !== JSON.stringify(initial);
    return (
        <>
            <Paper>
                <form onSubmit={submit} className="flex flex-col gap-3" aria-label={editing ? `Edit ${initial.id}` : "Add an action"}>
                    <ActionFields form={form} mistakes={mistakes} idLocked={editing} onChange={change} />
                    <FormFooter
                        saving={saving}
                        loose={loose}
                        editing={editing}
                        reloadLabel="Reload the actions"
                        onReload={onReload}
                        onCancel={onCancel}
                    />
                </form>
            </Paper>
            {testRun?.(unsaved)}
        </>
    );
}
