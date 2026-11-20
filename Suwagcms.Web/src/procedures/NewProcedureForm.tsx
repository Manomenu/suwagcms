import { useState } from "react";
import { useLocation, useSearch } from "wouter";

import { ApiError } from "@/api/client";
import { editHref, withListSearch } from "@/api/resourceAddress";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Paper } from "@/design/page";

import { saveProcedure } from "./api";
import { PROCEDURE_HINTS } from "./hints";

export function NewProcedureForm({ bot, onCancel }: { bot: string; onCancel: () => void }) {
    const [id, setId] = useState("");
    const [description, setDescription] = useState("");
    const [refusal, setRefusal] = useState<ApiError | null>(null);
    const [, navigate] = useLocation();
    const search = useSearch();
    return (
        <Paper className="max-w-2xl">
            <form
                className="flex flex-col gap-3"
                aria-label="New procedure"
                onSubmit={(event) => {
                    event.preventDefault();
                    saveProcedure(bot, id, { description, firstStep: null, steps: {}, whenStalled: null }, 0, false)
                        .then(() => {
                            navigate(withListSearch(editHref(bot, "procedures", id), search), { replace: true });
                        })
                        .catch((error: unknown) => {
                            setRefusal(error instanceof ApiError ? error : new ApiError(String(error)));
                        });
                }}
            >
                <Field prefix="new-procedure" name="id" label="Id" hint={PROCEDURE_HINTS.newId} errors={[]}>
                    {(props) => (
                        <Input
                            {...props}
                            value={id}
                            onChange={(event) => {
                                setId(event.target.value);
                            }}
                        />
                    )}
                </Field>
                <Field prefix="new-procedure" name="description" label="Description" hint={PROCEDURE_HINTS.description} errors={[]}>
                    {(props) => (
                        <Input
                            {...props}
                            value={description}
                            onChange={(event) => {
                                setDescription(event.target.value);
                            }}
                        />
                    )}
                </Field>
                {refusal !== null && (
                    <Alert variant="destructive">
                        {refusal.mistakes.length === 0
                            ? refusal.message
                            : refusal.mistakes.map((mistake) => `${mistake.message} ${mistake.fix}`).join(" ")}
                    </Alert>
                )}
                <div className="flex gap-2">
                    <Button type="submit">Create procedure</Button>
                    <Button type="button" variant="outline" onClick={onCancel}>
                        Cancel
                    </Button>
                </div>
            </form>
        </Paper>
    );
}
