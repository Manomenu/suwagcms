import { useResourceEditor } from "@/api/resourceEditor";
import { Alert } from "@/design/alert";
import { ResourceEdit } from "@/design/resourceEdit";

import { knowledgeBasesApi } from "./api";
import { emptyForm } from "./knowledgeBaseForm";
import { KnowledgeBaseForm } from "./KnowledgeBaseForm";
import { KnowledgeBasesView } from "./KnowledgeBasesView";

export function KnowledgeBasesScreen({ bot }: { bot: string }) {
    const editor = useResourceEditor({ bot, api: knowledgeBasesApi, idOf: (base) => base.id, usage: null });

    if (editor.failure !== null) return <Alert variant="destructive">{editor.failure}</Alert>;
    if (editor.items === null) return <p className="text-muted-foreground">Loading knowledge bases…</p>;
    if (!editor.isOpen) return <KnowledgeBasesView bot={bot} bases={editor.items} onAdd={editor.add} onEdit={editor.edit} />;

    return (
        <ResourceEdit noun="knowledge base" plural="knowledge bases" addTitle="Add a knowledge base" {...editor.panels}>
            <KnowledgeBaseForm
                key={editor.item?.id ?? "add"}
                bot={bot}
                initial={
                    editor.item === null ? emptyForm() : { id: editor.item.id, title: editor.item.title, language: editor.item.language }
                }
                {...editor.formProps}
            />
        </ResourceEdit>
    );
}
