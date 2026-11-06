import { useState } from "react";

import { ApiError, type Mistake } from "./client";

export interface SaveForm<Form> {
    form: Form;
    change: (change: Partial<Form>) => void;
    submit: (event: React.SyntheticEvent) => void;
    refusal: ApiError | null;
    mistakes: Mistake[];
    forgetRefusal: () => void;
}

/** What a form that saves a resource keeps: what was typed, which a refusal never loses, and the refusal itself. */
export function useSaveForm<Form>(initial: Form, save: (form: Form) => Promise<unknown>, onSaved: () => void): SaveForm<Form> {
    const [form, setForm] = useState(initial);
    const [refusal, setRefusal] = useState<ApiError | null>(null);
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
    };
}
