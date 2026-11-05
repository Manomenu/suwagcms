import type { AvailabilityState } from "@/api/availability";
import type { HistoryState } from "@/api/history";
import { usageHref, type UsageState } from "@/api/usage";
import { AvailabilityNotice, AvailabilityPanel } from "@/design/availabilityPanel";
import { Field } from "@/design/field";
import { HistoryPanel } from "@/design/historyPanel";
import { Input } from "@/design/input";
import { Textarea } from "@/design/textarea";
import { UsagePanel } from "@/design/usagePanel";

import type { Draft } from "./draft";
import { LocalFactsPanel } from "./LocalFactsPanel";

/** The fields of the procedure itself, shown when no step is selected. */
export function ProcedurePanel({
    bot,
    id,
    draft,
    usage,
    history,
    availability,
    edit,
}: {
    bot: string;
    id: string;
    draft: Draft;
    usage: UsageState;
    history: HistoryState;
    availability: AvailabilityState;
    edit: (change: (draft: Draft) => Draft) => void;
}) {
    // Why: every conversation is based on this procedure, so the server refuses to turn it off and there is no switch to press.
    const canBeOff = id !== "conversation";
    return (
        <section aria-label="Procedure" className="flex flex-col gap-3">
            <h2 className="text-base font-semibold">Procedure: {id}</h2>
            <AvailabilityNotice delivery={availability.delivery} failure={availability.failure} onSend={availability.send} />
            {canBeOff && (
                <AvailabilityPanel
                    id={id}
                    off={availability.isOff("procedure", id)}
                    delivery={availability.delivery}
                    usedBy={usage.usage?.usedBy ?? null}
                    onTurn={(off) => {
                        availability.turn("procedure", id, off);
                    }}
                />
            )}
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
            <LocalFactsPanel draft={draft} edit={edit} />
            <UsagePanel usage={usage.usage} failure={usage.failure} hrefOf={(entry) => usageHref(bot, entry)} />
            <HistoryPanel {...history} />
            <p className="text-sm text-muted-foreground">Select a step on the graph to edit it.</p>
        </section>
    );
}
