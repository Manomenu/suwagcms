import { useEffect, useState } from "react";

import { ApiError, type Mistake } from "./client";
import { DISCARD_QUESTION } from "./leaveGuard";

export interface SaveForm<Form> {
    form: Form;
    change: (change: Partial<Form>) => void;
    submit: (event: React.SyntheticEvent) => void;
    refusal: ApiError | null;
    mistakes: Mistake[];
    forgetRefusal: () => void;
    dirty: boolean;
    cancel: () => void;
}

function sameJson<Form>(form: Form, initial: Form): boolean {
    return JSON.stringify(form) === JSON.stringify(initial);
}

export function useSaveForm<Form>({
    initial,
    save,
    onSaved,
    onCancel,
    onDirtyChange,
    same = sameJson,
}: {
    initial: Form;
    save: (form: Form) => Promise<unknown>;
    onSaved: () => void;
    onCancel: () => void;
    onDirtyChange: (dirty: boolean) => void;
    same?: (form: Form, initial: Form) => boolean;
}): SaveForm<Form> {
    const [form, setForm] = useState(initial);
    const [refusal, setRefusal] = useState<ApiError | null>(null);
    const dirty = !same(form, initial);

    useEffect(() => {
        onDirtyChange(dirty);
        return () => {
            onDirtyChange(false);
        };
    }, [dirty, onDirtyChange]);

    return {
        form,
        change: (change) => {
            setForm((before) => ({ ...before, ...change }));
        },
        submit: (event) => {
            event.preventDefault();
            setRefusal(null);
            save(form)
                .then(onSaved)
                .catch((error: unknown) => {
                    setRefusal(error instanceof ApiError ? error : new ApiError(String(error)));
                });
        },
        refusal,
        mistakes: refusal?.mistakes ?? [],
        forgetRefusal: () => {
            setRefusal(null);
        },
        dirty,
        cancel: () => {
            if (!dirty || window.confirm(DISCARD_QUESTION)) onCancel();
        },
    };
}
