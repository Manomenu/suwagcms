import { Button } from "@/design/button";
import { Paper } from "@/design/page";

import { constraintOf } from "./actionForm";
import type { ActionField, ActionView } from "./api";

function Fields({ title, fields }: { title: string; fields: Record<string, ActionField> | null }) {
    const entries = Object.entries(fields ?? {});
    return (
        <section aria-label={title} className="flex flex-col gap-1 text-sm">
            <h3 className="font-semibold">{title}</h3>
            {entries.length === 0 ? (
                <p className="text-muted-foreground">None.</p>
            ) : (
                <ul className="flex flex-col gap-1">
                    {entries.map(([name, field]) => (
                        <li key={name}>
                            <span className="font-medium">{name}</span>{" "}
                            <span className="text-muted-foreground">
                                {constraintOf(field)} · {field.description}
                            </span>
                        </li>
                    ))}
                </ul>
            )}
        </section>
    );
}

export function CodeActionPanel({ action, onClose }: { action: ActionView; onClose: () => void }) {
    return (
        <Paper aria-label={`Code action ${action.id}`} role="region" className="flex flex-col gap-3">
            <h2 className="text-base font-semibold">{action.id}</h2>
            <p>{action.description}</p>
            <p className="text-sm text-muted-foreground">
                This action is written in the bot's code, so the CMS only shows it. Change it in the bot and deploy; the CMS sees the change
                the next time it asks.
            </p>
            <Fields title="Parameters" fields={action.parameters} />
            <Fields title="Results" fields={action.results} />
            <div>
                <Button type="button" variant="outline" onClick={onClose}>
                    Close
                </Button>
            </div>
        </Paper>
    );
}
