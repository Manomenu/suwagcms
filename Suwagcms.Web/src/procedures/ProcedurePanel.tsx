import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Textarea } from "@/design/textarea";

import type { Draft } from "./draft";

/** The fields of the procedure itself, shown when no step is selected. */
export function ProcedurePanel({ id, draft, edit }: { id: string; draft: Draft; edit: (change: (draft: Draft) => Draft) => void }) {
    return (
        <section aria-label="Procedure" className="flex flex-col gap-3">
            <h2 className="text-lg font-semibold">Procedure: {id}</h2>
            <Field
                prefix="procedure"
                name="description"
                label="Description"
                hint="What this procedure is for, in a sentence. A bot's other procedures are chosen by it."
                errors={[]}
            >
                {(props) => (
                    <Input
                        {...props}
                        value={draft.description}
                        onChange={(event) => {
                            edit((current) => ({ ...current, description: event.target.value }));
                        }}
                    />
                )}
            </Field>
            <Field
                prefix="procedure"
                name="whenStalled"
                label="When stalled"
                hint="What the bot does when the conversation gets stuck in this procedure: the instruction it follows."
                errors={[]}
            >
                {(props) => (
                    <Textarea
                        {...props}
                        value={draft.whenStalled ?? ""}
                        onChange={(event) => {
                            edit((current) => ({ ...current, whenStalled: event.target.value === "" ? null : event.target.value }));
                        }}
                    />
                )}
            </Field>
            <p className="text-sm text-muted-foreground">Select a step on the graph to edit it.</p>
        </section>
    );
}
