import { useEffect, useState } from "react";

import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { Paper } from "@/design/page";

import { fetchArticles, fetchProcedures, saveArticle, type KnowledgeBase } from "./api";
import type { LinkTarget } from "./articleLink";
import { bodyOf, fieldOf, isDirty, type ArticleForm as FormState } from "./articleForm";
import { ArticleFields } from "./ArticleFields";
import { linkTargets } from "./articlePicker";

export const DISCARD_QUESTION = "Discard the changes you have not saved?";

/**
 * The form that adds or edits an article. A refusal shows each mistake beside its field and keeps everything typed. `revision`
 * is the latest one the screen knows of the article being edited (0 when adding). Leaving with unsaved changes asks first.
 */
export function ArticleForm({
    bot,
    initial,
    bases,
    editing,
    revision,
    onSaved,
    onReload,
    onCancel,
    onDirtyChange,
    onOpenArticle,
}: {
    bot: string;
    initial: FormState;
    bases: KnowledgeBase[];
    editing: boolean;
    revision: number;
    onSaved: () => void;
    onReload: () => void;
    onCancel: () => void;
    onDirtyChange: (dirty: boolean) => void;
    onOpenArticle: (id: string, kind: "article" | "procedure") => void;
}) {
    const [form, setForm] = useState(initial);
    const [refusal, setRefusal] = useState<ApiError | null>(null);
    const [targets, setTargets] = useState<LinkTarget[] | null>(null);
    const [targetsFailure, setTargetsFailure] = useState<string | null>(null);
    const dirty = isDirty(form, initial);

    // Why: the picker and the chips need every article of the bot, not the filtered list the screen shows, and the guide's picker its procedures too.
    useEffect(() => {
        const controller = new AbortController();
        Promise.all([
            fetchArticles(bot, { knowledgeBase: "", language: "", text: "" }, controller.signal),
            fetchProcedures(bot, controller.signal),
        ])
            .then(([articles, list]) => {
                setTargets(linkTargets(articles, list.procedures));
            })
            .catch((error: unknown) => {
                if (!controller.signal.aborted) setTargetsFailure(error instanceof Error ? error.message : String(error));
            });
        return () => {
            controller.abort();
        };
    }, [bot]);

    useEffect(() => {
        onDirtyChange(dirty);
        return () => {
            onDirtyChange(false);
        };
    }, [dirty, onDirtyChange]);

    function submit(event: React.SyntheticEvent) {
        event.preventDefault();
        setRefusal(null);
        saveArticle(bot, form.id, bodyOf(form, initial), revision)
            .then(onSaved)
            .catch((error: unknown) => {
                setRefusal(error instanceof ApiError ? error : new ApiError(String(error)));
            });
    }

    const mistakes = refusal?.mistakes ?? [];
    const loose = mistakes.filter((mistake) => fieldOf(mistake, form.id) === null);
    const moved = mistakes.some((mistake) => mistake.code === "resource.stale");
    return (
        <Paper>
            <form onSubmit={submit} className="flex flex-col gap-3">
                {targetsFailure !== null && <Alert variant="destructive">{targetsFailure}</Alert>}
                {targets === null && targetsFailure === null && <p className="text-muted-foreground">Loading the form…</p>}
                {targets !== null && (
                    <ArticleFields
                        form={form}
                        bases={bases}
                        mistakes={mistakes}
                        idLocked={editing}
                        targets={targets}
                        onOpenArticle={onOpenArticle}
                        onChange={(change) => {
                            setForm((before) => ({ ...before, ...change }));
                        }}
                    />
                )}
                {refusal !== null && mistakes.length === 0 && <Alert variant="destructive">{refusal.message}</Alert>}
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
                    <Button type="submit">{editing ? "Save" : "Add"}</Button>
                    {moved && (
                        <Button
                            type="button"
                            variant="outline"
                            onClick={() => {
                                setRefusal(null);
                                onReload();
                            }}
                        >
                            Reload the article
                        </Button>
                    )}
                    <Button
                        type="button"
                        variant="outline"
                        onClick={() => {
                            if (!dirty || window.confirm(DISCARD_QUESTION)) onCancel();
                        }}
                    >
                        Cancel
                    </Button>
                </div>
            </form>
        </Paper>
    );
}
