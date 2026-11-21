import { Alert, Button, MistakeList, StickyActions, type Mistake } from "@maniumek/suwfrontkit";

export function FormFooter({
    saving,
    loose,
    editing,
    reloadLabel,
    onReload,
}: {
    saving: {
        pending: boolean;
        refusal: { message: string } | null;
        mistakes: readonly Mistake[];
        forgetRefusal: () => void;
        cancel: () => void;
    };
    loose: readonly Mistake[];
    editing: boolean;
    reloadLabel: string;
    onReload: () => void;
}) {
    const { refusal } = saving;
    const moved = saving.mistakes.some((mistake) => mistake.code === "resource.stale");
    return (
        <>
            {refusal !== null && saving.mistakes.length === 0 && <Alert variant="destructive">{refusal.message}</Alert>}
            {loose.length > 0 && (
                <Alert variant="destructive">
                    <MistakeList mistakes={loose} />
                </Alert>
            )}
            <StickyActions>
                <Button type="submit" disabled={saving.pending}>
                    {editing ? "Save" : "Add"}
                </Button>
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
                <Button type="button" variant="outline" onClick={saving.cancel}>
                    Cancel
                </Button>
            </StickyActions>
        </>
    );
}
