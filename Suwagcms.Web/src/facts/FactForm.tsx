import { useState } from "react";

import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
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
    const [form, setForm] = useState(initial);
    const [refusal, setRefusal] = useState<ApiError | null>(null);

    function submit(event: React.SyntheticEvent) {
        event.preventDefault();
        setRefusal(null);
        saveFact(bot, form.name, bodyOf(form), revision)
            .then(onSaved)
            .catch((error: unknown) => {
                setRefusal(error instanceof ApiError ? error : new ApiError(String(error)));
            });
    }

    const mistakes = refusal?.mistakes ?? [];
    const loose = mistakes.filter((mistake) => fieldOf(mistake) === null);
    const moved = mistakes.some((mistake) => mistake.code === "resource.stale");
    return (
        <Paper>
            <form onSubmit={submit} className="flex max-w-2xl flex-col gap-3">
                <FactFields
                    bot={bot}
                    form={form}
                    mistakes={mistakes}
                    nameLocked={editing}
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
                            Reload the facts
                        </Button>
                    )}
                    <Button type="button" variant="outline" onClick={onCancel}>
                        Cancel
                    </Button>
                </div>
            </form>
        </Paper>
    );
}
