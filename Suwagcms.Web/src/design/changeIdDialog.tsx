import { Button, Dialog, Field, Input } from "@maniumek/suwfrontkit";
import { useEffect, useState } from "react";

interface IdAnswer {
    lines: { id: string; where: string }[];
    mistakes: string[];
}

const QUIET_MS = 300;

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
        <Dialog
            open
            onOpenChange={(open) => {
                if (!open) onCancel();
            }}
            title={`Change ${idWord} of ${noun} ${id}`}
            actions={
                <>
                    <Button type="button" variant="outline" onClick={onCancel}>
                        Cancel
                    </Button>
                    <Button type="button" disabled={current === null || current.mistakes.length > 0} onClick={confirm}>
                        Change {idWord}
                    </Button>
                </>
            }
        >
            <div className="flex flex-col gap-3">
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
            </div>
        </Dialog>
    );
}
