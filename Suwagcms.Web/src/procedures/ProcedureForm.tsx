import { useMemo, type ReactNode } from "react";

import type { Mistake } from "@/api/client";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { Field, Legend } from "@/design/field";
import { Input } from "@/design/input";
import { Select } from "@/design/select";
import { Textarea } from "@/design/textarea";

import { factsSeen, withStallText } from "./draft";
import { KnowledgeForm } from "./KnowledgeForm";
import { LocalFactsPanel } from "./LocalFactsPanel";
import { PROCEDURE_HINTS } from "./hints";
import { consequenceOf, placeOf, stepOfPlace, type Check } from "./mistakes";
import { StepsEditor, type StepsProps } from "./StepsEditor";

function MistakeNote({ mistake }: { mistake: Mistake }) {
    return (
        <div className="rounded-md border border-border p-2 text-sm" data-mistake={mistake.code}>
            <span
                className={`mr-2 inline-flex rounded-md px-2 py-0.5 text-xs font-semibold ${
                    mistake.stage === "Save" ? "bg-destructive/15 text-destructive" : "bg-warning/15 text-warning"
                }`}
            >
                {consequenceOf(mistake)}
            </span>
            {mistake.message} <span className="text-muted-foreground">{mistake.fix}</span>
        </div>
    );
}

/** Says how many mistakes there are; each jumps to its place, so a long page need not be searched. */
function MistakeCount({
    done,
    mistakes,
    onPick,
}: {
    done: boolean;
    mistakes: { mistake: Mistake; place: string }[];
    onPick: (place: string) => void;
}) {
    if (mistakes.length === 0) return done ? <p className="text-sm text-muted-foreground">No mistakes: ready for a version.</p> : null;
    const refusing = mistakes.filter(({ mistake }) => mistake.stage === "Save").length;
    return (
        <section aria-label="Mistakes" className="flex flex-col gap-1 rounded-md border border-border p-3 text-sm">
            <p className="font-semibold">
                {mistakes.length} {mistakes.length === 1 ? "mistake" : "mistakes"}{" "}
                <span className="font-normal text-muted-foreground">
                    {refusing} {refusing === 1 ? "refuses" : "refuse"} a save, {mistakes.length - refusing}{" "}
                    {mistakes.length - refusing === 1 ? "stops" : "stop"} a version
                </span>
            </p>
            <ul className="flex flex-col gap-1">
                {mistakes.map(({ mistake, place }) => (
                    <li key={`${mistake.code}:${mistake.path}`}>
                        {place === "top" ? (
                            <MistakeNote mistake={mistake} />
                        ) : (
                            <button
                                type="button"
                                className="cursor-pointer text-left text-anchor underline underline-offset-4"
                                onClick={() => {
                                    onPick(place);
                                }}
                            >
                                {mistake.message}
                            </button>
                        )}
                    </li>
                ))}
            </ul>
        </section>
    );
}

/** The kind's form of the shared edit screen: the procedure's settings, its steps, and Save at the end, as on every kind. */
export function ProcedureForm({
    check,
    dirty,
    saving,
    canBeInterrupted,
    onSave,
    note,
    allFacts,
    ...steps
}: Omit<StepsProps, "beside" | "facts"> & {
    check: Check;
    dirty: boolean;
    saving: boolean;
    canBeInterrupted: boolean;
    onSave: () => void;
    note: string | null;
    allFacts: StepsProps["facts"];
}) {
    const { draft, edit, id, onSelect, showKnowledge, bases, articles } = steps;
    const placed = useMemo(
        () => (check.state === "done" ? check.mistakes.map((mistake) => ({ mistake, place: placeOf(mistake, id, draft) })) : []),
        [check, id, draft],
    );
    const beside = (place: string): ReactNode => {
        const here = placed.filter((one) => one.place === place);
        return here.length === 0 ? null : (
            <div className="flex flex-col gap-1">
                {here.map(({ mistake }) => (
                    <MistakeNote key={`${mistake.code}:${mistake.path}`} mistake={mistake} />
                ))}
            </div>
        );
    };
    return (
        <div className="flex flex-col gap-4">
            <MistakeCount
                done={check.state === "done"}
                mistakes={placed}
                onPick={(place) => {
                    const step = stepOfPlace(place);
                    if (step !== null) onSelect(step);
                    document.querySelector(`[data-place="${CSS.escape(place)}"]`)?.scrollIntoView({ block: "center" });
                }}
            />
            {check.state === "unreachable" && <Alert variant="destructive">{check.message}</Alert>}
            <div className="flex flex-col gap-3" data-place="field:description">
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
                {beside("field:description")}
            </div>
            <div className="flex flex-col gap-1" data-place="field:localFacts">
                <LocalFactsPanel draft={draft} edit={edit} />
                {beside("field:localFacts")}
            </div>
            <div className="flex flex-col gap-2" data-place="field:whenStalled">
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
                {beside("field:whenStalled")}
                {draft.whenStalled && (
                    <KnowledgeForm
                        owner="whenStalled"
                        show={showKnowledge}
                        knowledge={draft.whenStalled.knowledge ?? []}
                        bases={bases}
                        articles={articles}
                        beside={beside}
                        onChange={(knowledge) => {
                            edit((current) => {
                                if (!current.whenStalled) return current;
                                const { knowledge: _, ...rest } = current.whenStalled;
                                return { ...current, whenStalled: knowledge.length === 0 ? rest : { ...rest, knowledge } };
                            });
                        }}
                    />
                )}
            </div>
            {canBeInterrupted && (
                <div className="flex flex-col gap-1" data-place="field:whenInterrupted">
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
                    {beside("field:whenInterrupted")}
                </div>
            )}
            <section aria-label="Steps" className="flex flex-col gap-2">
                <Legend hint={PROCEDURE_HINTS.steps}>Steps</Legend>
                {note !== null && <p className="text-sm text-muted-foreground">{note}</p>}
                <StepsEditor {...steps} facts={factsSeen(draft, allFacts)} beside={beside} />
            </section>
            <div className="flex items-center justify-end gap-2">
                {dirty && <span className="text-sm text-warning">Unsaved changes</span>}
                <Button type="button" onClick={onSave} disabled={saving || !dirty}>
                    Save
                </Button>
            </div>
        </div>
    );
}
