import { useState } from "react";

import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
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
    const [form, setForm] = useState(initial);
    const [refusal, setRefusal] = useState<ApiError | null>(null);

    function submit(event: React.SyntheticEvent) {
        event.preventDefault();
        setRefusal(null);
        saveAction(bot, form.id, bodyOf(form), revision)
            .then(onSaved)
            .catch((error: unknown) => {
                setRefusal(error instanceof ApiError ? error : new ApiError(String(error)));
            });
    }

    const mistakes = refusal?.mistakes ?? [];
    const places = placesOf(form);
    const loose = mistakes.filter((mistake) => !places.includes(placeOf(mistake)));
    const moved = mistakes.some((mistake) => mistake.code === "resource.stale");
    const unsaved = JSON.stringify(form) !== JSON.stringify(initial);
    return (
        <>
            <Paper>
                <form onSubmit={submit} className="flex flex-col gap-3" aria-label={editing ? `Edit ${initial.id}` : "Add an action"}>
                    <ActionFields
                        form={form}
                        mistakes={mistakes}
                        idLocked={editing}
                        onChange={(change) => {
                            setForm((before) => ({ ...before, ...change }));
                        }}
                    />
                    {refusal !== null && mistakes.length === 0 && <Alert variant="destructive">{refusal.message}</Alert>}
                    {loose.length > 0 && (
                        <Alert variant="destructive">
                            <ul className="list-disc pl-4">
                                {loose.map((mistake) => (
                                    <li key={`${mistake.code}:${mistake.path}`}>
                                        {mistake.message} {mistake.fix}
                                    </li>
                                ))}
                            </ul>
                        </Alert>
                    )}
                    <div className="flex gap-2">
                        <Button type="submit">{editing ? "Save" : "Add"}</Button>
                        {moved && (
                            <Button
                                type="button"
                                variant="outline"
                                onClick={() => {
                                    setRefusal(null);
                                    onReload();
                                }}
                            >
                                Reload the actions
                            </Button>
                        )}
                        <Button type="button" variant="outline" onClick={onCancel}>
                            Cancel
                        </Button>
                    </div>
                </form>
            </Paper>
            {testRun?.(unsaved)}
        </>
    );
}
