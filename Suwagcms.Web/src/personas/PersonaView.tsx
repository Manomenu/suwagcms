import type { Mistake } from "@/api/client";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { Field } from "@/design/field";
import { InfoTip } from "@/design/infoTip";
import { Page, Paper, type HistoryPanelProps } from "@/design/page";
import { Textarea } from "@/design/textarea";

import { HINTS, fieldOf, type FieldName, type PersonaForm } from "./personaForm";

export function PersonaView({
    bot,
    form,
    revision,
    empty,
    saved,
    saving,
    failure,
    mistakes,
    outOfDate,
    history,
    onChange,
    onSave,
    onReload,
}: {
    bot: string;
    form: PersonaForm;
    revision: number;
    empty: boolean;
    saved: boolean;
    saving: boolean;
    failure: string | null;
    mistakes: Mistake[];
    outOfDate: boolean;
    history: HistoryPanelProps;
    onChange: (name: FieldName, text: string) => void;
    onSave: () => void;
    onReload: () => void;
}) {
    function text(name: FieldName, label: string, shown: { hint: string } | { labelHidden: true }) {
        const errors = mistakes.filter((mistake) => fieldOf(mistake) === name).map((mistake) => `${mistake.message} ${mistake.fix}`);
        return (
            <Field prefix="persona" name={name} label={label} {...shown} errors={errors}>
                {(props) => (
                    <Textarea
                        {...props}
                        className="h-32 resize-y"
                        value={form[name]}
                        onChange={(event) => {
                            onChange(name, event.currentTarget.value);
                        }}
                    />
                )}
            </Field>
        );
    }

    const loose = mistakes.filter((mistake) => fieldOf(mistake) === null);
    return (
        <Page
            title={`Persona of ${bot}`}
            history={history}
            actions={
                <>
                    {saved && <span className="text-sm text-success">Saved</span>}
                    <span className="text-sm text-muted-foreground">revision {revision}</span>
                    <Button onClick={onSave} disabled={saving}>
                        Save
                    </Button>
                </>
            }
        >
            {empty && (
                <p className="text-muted-foreground">This persona is empty, so a version holds none. Write in the fields and save.</p>
            )}
            {failure !== null && <Alert variant="destructive">{failure}</Alert>}
            {loose.length > 0 && (
                <Alert variant="destructive">
                    <ul className="flex flex-col gap-1">
                        {loose.map((mistake) => (
                            <li key={`${mistake.code}:${mistake.path}`}>
                                {mistake.message} {mistake.fix}
                            </li>
                        ))}
                    </ul>
                </Alert>
            )}
            {outOfDate && (
                <div>
                    <Button variant="outline" onClick={onReload}>
                        Reload the persona
                    </Button>
                </div>
            )}
            <Paper className="flex flex-col gap-3">
                <Section title="Personality" subtitle="how it talks" hint={HINTS.personality} />
                {text("personality", "Personality", { labelHidden: true })}
            </Paper>
            <Paper className="flex flex-col gap-3">
                <Section title="Scope" subtitle="what it talks about" hint={HINTS.scope} />
                {text("inScope", "In scope", { hint: HINTS.inScope })}
                {text("outOfScope", "Out of scope", { hint: HINTS.outOfScope })}
                {text("whenOutOfScope", "When out of scope", { hint: HINTS.whenOutOfScope })}
            </Paper>
        </Page>
    );
}

function Section({ title, subtitle, hint }: { title: string; subtitle: string; hint: string }) {
    return (
        <h2 className="flex items-center gap-2 text-xl font-bold">
            {title}
            <span className="text-sm font-normal text-muted-foreground">{subtitle}</span>
            <InfoTip about={title} help={hint} />
        </h2>
    );
}
