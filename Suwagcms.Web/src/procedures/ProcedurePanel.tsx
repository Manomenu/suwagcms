import type { AvailabilityState } from "@/api/availability";
import type { HistoryState } from "@/api/history";
import { usageHref, type UsageState } from "@/api/usage";
import { AvailabilityNotice, AvailabilityPanel } from "@/design/availabilityPanel";
import { DeleteAction } from "@/design/deleteAction";
import { Field } from "@/design/field";
import { HistoryPanel } from "@/design/historyPanel";
import { Input } from "@/design/input";
import { Select } from "@/design/select";
import { Textarea } from "@/design/textarea";
import { UsagePanel } from "@/design/usagePanel";

import { ContextForm } from "./ContextForm";
import { PROCEDURE_HINTS } from "./hints";
import { withStallText, type Draft, type LinkableArticle } from "./draft";
import { LocalFactsPanel } from "./LocalFactsPanel";

/** The fields of the procedure itself, shown when no step is selected. */
export function ProcedurePanel({
    bot,
    id,
    draft,
    articles,
    usage,
    history,
    availability,
    deleteFailure,
    onDelete,
    edit,
}: {
    bot: string;
    id: string;
    draft: Draft;
    articles: LinkableArticle[];
    usage: UsageState;
    history: HistoryState;
    availability: AvailabilityState;
    deleteFailure: string | null;
    onDelete: () => void;
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
            <Field prefix="procedure" name="description" label="Description" hint={PROCEDURE_HINTS.description} errors={[]}>
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
            <Field prefix="procedure" name="whenStalled" label="When stalled" hint={PROCEDURE_HINTS.whenStalled} errors={[]}>
                {(props) => (
                    <Textarea
                        {...props}
                        value={draft.whenStalled?.instruction ?? ""}
                        onChange={(event) => {
                            edit((current) => ({ ...current, whenStalled: withStallText(current.whenStalled, event.target.value) }));
                        }}
                    />
                )}
            </Field>
            {draft.whenStalled && (
                <ContextForm
                    context={draft.whenStalled.context}
                    articles={articles}
                    onChange={(context) => {
                        edit((current) =>
                            current.whenStalled ? { ...current, whenStalled: { ...current.whenStalled, context } } : current,
                        );
                    }}
                />
            )}
            {canBeOff && (
                <Field
                    prefix="procedure"
                    name="whenInterrupted"
                    label="When interrupted"
                    hint={PROCEDURE_HINTS.whenInterrupted}
                    errors={[]}
                >
                    {(props) => (
                        <Select
                            {...props}
                            className="w-full"
                            value={draft.whenInterrupted ?? "keep"}
                            onChange={(event) => {
                                const whenInterrupted = event.currentTarget.value;
                                edit((current) => ({ ...current, whenInterrupted }));
                            }}
                        >
                            <option value="keep">keep</option>
                            <option value="discard">discard</option>
                        </Select>
                    )}
                </Field>
            )}
            <LocalFactsPanel draft={draft} edit={edit} />
            <UsagePanel usage={usage.usage} failure={usage.failure} hrefOf={(entry) => usageHref(bot, entry)} />
            {canBeOff && usage.usage !== null && (
                <DeleteAction noun="procedure" id={id} usedBy={usage.usage.usedBy.length} failure={deleteFailure} onDelete={onDelete} />
            )}
            <HistoryPanel {...history} />
            <p className="text-sm text-muted-foreground">Select a step on the graph to edit it.</p>
        </section>
    );
}
