import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { MistakeItems, type ShownMistake } from "@/design/mistakeItems";

/**
 * The end of a resource's form: a refusal no field shows, the save button, and Cancel. A resource that moved on since it was
 * opened offers `reloadLabel`.
 */
export function FormFooter({
    saving,
    loose,
    editing,
    reloadLabel,
    onReload,
    onCancel,
}: {
    saving: { refusal: { message: string; mistakes: ShownMistake[] } | null; forgetRefusal: () => void };
    loose: ShownMistake[];
    editing: boolean;
    reloadLabel: string;
    onReload: () => void;
    onCancel: () => void;
}) {
    const { refusal } = saving;
    const moved = refusal?.mistakes.some((mistake) => mistake.code === "resource.stale") ?? false;
    return (
        <>
            {refusal !== null && refusal.mistakes.length === 0 && <Alert variant="destructive">{refusal.message}</Alert>}
            {loose.length > 0 && (
                <Alert variant="destructive">
                    <MistakeItems mistakes={loose} />
                </Alert>
            )}
            <div className="flex gap-2">
                <Button type="submit">{editing ? "Save" : "Add"}</Button>
                {moved && (
                    <Button
                        type="button"
                        variant="outline"
                        onClick={() => {
                            saving.forgetRefusal();
                            onReload();
                        }}
                    >
                        {reloadLabel}
                    </Button>
                )}
                <Button type="button" variant="outline" onClick={onCancel}>
                    Cancel
                </Button>
            </div>
        </>
    );
}
