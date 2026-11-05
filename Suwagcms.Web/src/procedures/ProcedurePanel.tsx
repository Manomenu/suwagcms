import { usageHref, type UsageState } from "@/api/usage";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Textarea } from "@/design/textarea";
import { UsagePanel } from "@/design/usagePanel";

import { ContextForm } from "./ContextForm";
import { withStallText, type Draft, type LinkableArticle } from "./draft";

/** The fields of the procedure itself, shown when no step is selected. */
export function ProcedurePanel({
    bot,
    id,
    draft,
    articles,
    usage,
    edit,
}: {
    bot: string;
    id: string;
    draft: Draft;
    articles: LinkableArticle[];
    usage: UsageState;
    edit: (change: (draft: Draft) => Draft) => void;
}) {
    return (
        <section aria-label="Procedure" className="flex flex-col gap-3">
            <h2 className="text-base font-semibold">Procedure: {id}</h2>
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
            <UsagePanel usage={usage.usage} failure={usage.failure} hrefOf={(entry) => usageHref(bot, entry)} />
            <p className="text-sm text-muted-foreground">Select a step on the graph to edit it.</p>
        </section>
    );
}
