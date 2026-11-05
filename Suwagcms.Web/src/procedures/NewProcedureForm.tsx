import { useState } from "react";
import { useLocation } from "wouter";

import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Paper } from "@/design/page";

import { saveProcedure } from "./api";

/** Starts a procedure: an id and a description save an empty one, and the creator opens on it. */
export function NewProcedureForm({ bot }: { bot: string }) {
    const [id, setId] = useState("");
    const [description, setDescription] = useState("");
    const [refusal, setRefusal] = useState<ApiError | null>(null);
    const [, navigate] = useLocation();
    return (
        <Paper className="max-w-2xl">
            <form
                className="flex flex-col gap-3"
                aria-label="New procedure"
                onSubmit={(event) => {
                    event.preventDefault();
                    saveProcedure(bot, id, { description, firstStep: null, steps: {}, whenStalled: null }, 0, false)
                        .then(() => {
                            navigate(`/bots/${encodeURIComponent(bot)}/procedures/${encodeURIComponent(id)}`);
                        })
                        .catch((error: unknown) => {
                            setRefusal(error instanceof ApiError ? error : new ApiError(String(error)));
                        });
                }}
            >
                <Field
                    prefix="new-procedure"
                    name="id"
                    label="Id"
                    hint="One camelCase word, like orderValuation. It cannot change."
                    errors={[]}
                >
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
                <Field
                    prefix="new-procedure"
                    name="description"
                    label="Description"
                    hint="What the procedure is for, in a sentence."
                    errors={[]}
                >
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
                <Button type="submit" className="self-start">
                    Create procedure
                </Button>
            </form>
        </Paper>
    );
}
