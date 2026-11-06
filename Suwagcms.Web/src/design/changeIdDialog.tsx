import { useEffect, useState } from "react";

import { Button } from "@/design/button";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Paper } from "@/design/page";

/** What the server answers for an id: each referrer's field that would be rewritten, or what refuses it. */
interface IdAnswer {
    lines: { id: string; where: string }[];
    mistakes: string[];
}

const QUIET_MS = 300;

/**
 * Asks for a new id, and lists what would be rewritten before anything changes. The server's dry run says what is wrong with the id,
 * so the dialog holds no rule of its own; the change goes through only after an answer that refuses nothing.
 */
export function ChangeIdDialog({
    noun,
    idWord = "id",
    id,
    preview,
    apply,
    onChanged,
    onCancel,
}: {
    noun: string;
    /** What the kind calls its id: a fact has a name. */
    idWord?: string;
    id: string;
    preview: (newId: string, signal: AbortSignal) => Promise<IdAnswer>;
    apply: (newId: string) => Promise<IdAnswer>;
    onChanged: (newId: string) => void;
    onCancel: () => void;
}) {
    const [newId, setNewId] = useState("");
    const [answer, setAnswer] = useState<{ newId: string; shown: IdAnswer } | null>(null);
    const [refusal, setRefusal] = useState<string[]>([]);

    useEffect(() => {
        if (newId === "") return undefined;
        const controller = new AbortController();
        const timer = setTimeout(() => {
            void preview(newId, controller.signal).then((shown) => {
                if (!controller.signal.aborted) setAnswer({ newId, shown });
            });
        }, QUIET_MS);
        return () => {
            clearTimeout(timer);
            controller.abort();
        };
    }, [newId, preview]);

    const current = answer?.newId === newId ? answer.shown : null;
    const referrers = new Set(current?.lines.map((line) => line.id)).size;

    const confirm = () => {
        setRefusal([]);
        void apply(newId).then((done) => {
            if (done.mistakes.length === 0) onChanged(newId);
            else setRefusal(done.mistakes);
        });
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-background/70 p-4">
            <Paper role="dialog" aria-label={`Change ${idWord}`} className="flex w-full max-w-lg flex-col gap-3">
                <h2 className="text-base font-semibold">
                    Change {idWord} of {noun} {id}
                </h2>
                <Field
                    prefix="change-id"
                    name="newId"
                    label={`New ${idWord}`}
                    hint={`The ${idWord} this ${noun} will have. The answer below says whether it is free and in the right form, and what would be rewritten.`}
                    errors={[...(current?.mistakes ?? []), ...refusal]}
                >
                    {(props) => (
                        <Input
                            {...props}
                            autoFocus
                            value={newId}
                            onChange={(event) => {
                                setNewId(event.target.value);
                                setRefusal([]);
                            }}
                        />
                    )}
                </Field>
                {current !== null && current.mistakes.length === 0 && (
                    <section aria-label="What is rewritten" className="flex flex-col gap-1 text-sm">
                        <p className="font-medium">
                            {referrers === 0
                                ? "This rewrites nothing else."
                                : `This rewrites ${referrers} ${referrers === 1 ? "referrer" : "referrers"}`}
                        </p>
                        <ul className="flex flex-col gap-1">
                            {current.lines.map((line) => (
                                <li key={`${line.id}:${line.where}`}>
                                    {line.id} <span className="text-muted-foreground">{line.where}</span>
                                </li>
                            ))}
                        </ul>
                    </section>
                )}
                <p className="text-sm text-muted-foreground">Plain text naming this {noun} is not changed.</p>
                <div className="flex justify-end gap-2">
                    <Button type="button" variant="outline" onClick={onCancel}>
                        Cancel
                    </Button>
                    <Button type="button" disabled={current === null || current.mistakes.length > 0} onClick={confirm}>
                        Change {idWord}
                    </Button>
                </div>
            </Paper>
        </div>
    );
}
