import { useEffect, useState } from "react";

import { useSaveForm } from "@/api/saveForm";
import { Alert } from "@/design/alert";
import { FormFooter } from "@/design/formFooter";
import { Paper } from "@/design/page";

import { articlesApi, fetchArticles, fetchProcedures, type KnowledgeBase } from "./api";
import type { LinkTarget } from "./articleLink";
import { bodyOf, fieldOf, isDirty, type ArticleForm as FormState } from "./articleForm";
import { ArticleFields } from "./ArticleFields";
import { linkTargets } from "./articlePicker";

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
    const saving = useSaveForm({
        initial,
        save: (form: FormState) => articlesApi.save(bot, form.id, bodyOf(form, initial), revision),
        onSaved,
        onCancel,
        onDirtyChange,
        same: (form, before) => !isDirty(form, before),
    });
    const { form, change, submit, mistakes } = saving;
    const [targets, setTargets] = useState<LinkTarget[] | null>(null);
    const [targetsFailure, setTargetsFailure] = useState<string | null>(null);

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

    const loose = mistakes.filter((mistake) => fieldOf(mistake, form.id) === null);
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
                        onChange={change}
                    />
                )}
                <FormFooter saving={saving} loose={loose} editing={editing} reloadLabel="Reload the article" onReload={onReload} />
            </form>
        </Paper>
    );
}
