import { useResourceEditor } from "@/api/resourceEditor";
import { Alert } from "@/design/alert";
import { EditorPanels } from "@/design/editorPanels";

import { knowledgeBasesApi } from "./api";
import { emptyForm } from "./knowledgeBaseForm";
import { KnowledgeBaseForm } from "./KnowledgeBaseForm";
import { KnowledgeBasesView } from "./KnowledgeBasesView";

export function KnowledgeBasesScreen({ bot }: { bot: string }) {
    const editor = useResourceEditor({ bot, api: knowledgeBasesApi, idOf: (base) => base.id, usage: null });

    if (editor.failure !== null) return <Alert variant="destructive">{editor.failure}</Alert>;
    if (editor.items === null) return <p className="text-muted-foreground">Loading knowledge bases…</p>;

    return (
        <KnowledgeBasesView
            bot={bot}
            bases={editor.items}
            onAdd={editor.add}
            onEdit={editor.edit}
            onDelete={editor.remove}
            form={
                <EditorPanels noun="knowledge base" {...editor.panels}>
                    {editor.isOpen && (
                        <KnowledgeBaseForm
                            key={editor.item?.id ?? "add"}
                            bot={bot}
                            initial={
                                editor.item === null
                                    ? emptyForm()
                                    : { id: editor.item.id, title: editor.item.title, language: editor.item.language }
                            }
                            {...editor.formProps}
                        />
                    )}
                </EditorPanels>
            }
        />
    );
}
