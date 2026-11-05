import type { ReactNode } from "react";

import { Label } from "@/design/label";

/**
 * One form field: its label (with a tooltip when `hint` is given), the control, and the mistakes written beneath it. The control
 * gets `props`, which tie it to the label and mark it invalid for assistive technology. `action` is a button on the label row,
 * right-aligned; `labelHidden` keeps the label for assistive technology where a heading already says it; fields side by side that should line up pass it too, `null` when they have no button, so the row is as tall as the button's.
 */
export function Field({
    prefix,
    name,
    label,
    labelHidden,
    hint,
    action,
    errors,
    children,
}: {
    prefix: string;
    name: string;
    label: string;
    labelHidden?: boolean;
    hint?: string;
    action?: ReactNode;
    errors: string[];
    children: (props: object) => ReactNode;
}) {
    const id = `${prefix}-${name}`;
    const heading = (
        <Label htmlFor={id} title={hint} className={labelHidden === true ? "sr-only" : hint === undefined ? undefined : "cursor-help"}>
            {label}
            {hint !== undefined && (
                <span aria-hidden="true" className="text-muted-foreground">
                    ⓘ
                </span>
            )}
        </Label>
    );
    return (
        <div data-field={name} className="flex flex-col gap-2">
            {action === undefined ? (
                heading
            ) : (
                <div className="flex min-h-[30px] items-center justify-between gap-2">
                    {heading}
                    {action}
                </div>
            )}
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
