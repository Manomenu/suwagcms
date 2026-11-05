import { useState } from "react";

import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Paper } from "@/design/page";

import { saveKnowledgeBase } from "./api";
import { fieldOf, mistakesFor, type FieldName, type KnowledgeBaseForm as FormState } from "./knowledgeBaseForm";

/**
 * The form that adds a knowledge base or renames one. A refusal shows each mistake beside its field and keeps everything typed.
 * `revision` is the latest one the screen knows of the base being renamed (0 when adding).
 */
export function KnowledgeBaseForm({
    bot,
    initial,
    editing,
    revision,
    onSaved,
    onReload,
    onCancel,
}: {
    bot: string;
    initial: FormState;
    editing: boolean;
    revision: number;
    onSaved: () => void;
    onReload: () => void;
    onCancel: () => void;
}) {
    const [form, setForm] = useState(initial);
    const [refusal, setRefusal] = useState<ApiError | null>(null);

    function submit(event: React.SyntheticEvent) {
        event.preventDefault();
        setRefusal(null);
        saveKnowledgeBase(bot, form.id, { title: form.title }, revision)
            .then(onSaved)
            .catch((error: unknown) => {
                setRefusal(error instanceof ApiError ? error : new ApiError(String(error)));
            });
    }

    const mistakes = mistakesFor(refusal?.mistakes ?? [], form.id);
    const errorsOf = (field: FieldName) =>
        mistakes.filter((mistake) => fieldOf(mistake) === field).map((mistake) => `${mistake.message} ${mistake.fix}`);
    const loose = mistakes.filter((mistake) => fieldOf(mistake) === null);
    const moved = mistakes.some((mistake) => mistake.code === "resource.stale");
    return (
        <Paper>
            <form onSubmit={submit} className="flex max-w-2xl flex-col gap-3">
                <Field prefix="knowledge-base" name="id" label="Id" errors={errorsOf("id")}>
                    {(props) => (
                        <Input
                            {...props}
                            value={form.id}
                            disabled={editing}
                            placeholder="offer"
                            onChange={(event) => {
                                setForm({ ...form, id: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
                <Field prefix="knowledge-base" name="title" label="Title" errors={errorsOf("title")}>
                    {(props) => (
                        <Input
                            {...props}
                            value={form.title}
                            placeholder="Our offer"
                            onChange={(event) => {
                                setForm({ ...form, title: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
                {refusal !== null && refusal.mistakes.length === 0 && <Alert variant="destructive">{refusal.message}</Alert>}
                {loose.length > 0 && (
                    <Alert variant="destructive">
                        <ul className="list-disc pl-4">
                            {loose.map((mistake) => (
                                <li key={`${mistake.code}:${mistake.path}`}>
                                    {mistake.message} {mistake.fix}
                                </li>
                            ))}
                        </ul>
                    </Alert>
                )}
                <div className="flex gap-2">
                    <Button type="submit">{editing ? "Save" : "Create"}</Button>
                    {moved && (
                        <Button
                            type="button"
                            variant="outline"
                            onClick={() => {
                                setRefusal(null);
                                onReload();
                            }}
                        >
                            Reload the knowledge bases
                        </Button>
                    )}
                    <Button type="button" variant="outline" onClick={onCancel}>
                        Cancel
                    </Button>
                </div>
            </form>
        </Paper>
    );
}
