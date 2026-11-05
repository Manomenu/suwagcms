import type { ReactNode } from "react";

import { Label } from "@/design/label";

/**
 * One form field: its label (with a tooltip when `hint` is given), the control, and the mistakes written beneath it. The control
 * gets `props`, which tie it to the label and mark it invalid for assistive technology.
 */
export function Field({
    prefix,
    name,
    label,
    hint,
    errors,
    children,
}: {
    prefix: string;
    name: string;
    label: string;
    hint?: string;
    errors: string[];
    children: (props: object) => ReactNode;
}) {
    const id = `${prefix}-${name}`;
    return (
        <div data-field={name} className="flex flex-col gap-2">
            <Label htmlFor={id} title={hint} className={hint === undefined ? undefined : "cursor-help"}>
                {label}
                {hint !== undefined && (
                    <span aria-hidden="true" className="text-muted-foreground">
                        ⓘ
                    </span>
                )}
            </Label>
            {children({
                id,
                "aria-invalid": errors.length > 0 || undefined,
                "aria-describedby": errors.length > 0 ? `${id}-mistakes` : undefined,
            })}
            {errors.length > 0 && (
                <ul id={`${id}-mistakes`} className="list-disc pl-4 text-sm text-destructive">
                    {errors.map((error) => (
                        <li key={error}>{error}</li>
                    ))}
                </ul>
            )}
        </div>
    );
}
