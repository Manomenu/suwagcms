import { Alert, ApiError, Button, Field, Input, Paper } from "@maniumek/suwfrontkit";
import { mistakesOf, refusalOf } from "@/api/client";
import { useState } from "react";
import { useLocation, useSearch } from "wouter";

import { editHref, withListSearch } from "@/api/resourceAddress";

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
                            setRefusal(refusalOf(error));
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
                        {mistakesOf(refusal).length === 0
                            ? refusal.message
                            : mistakesOf(refusal)
                                  .map((mistake) => `${mistake.message} ${mistake.fix}`)
                                  .join(" ")}
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
