import type { ReactNode } from "react";

import { InfoTip } from "@/design/infoTip";
import { Label } from "@/design/label";

/**
 * One form field: its label with its help, the control, and the mistakes written beneath it. The control gets `props`, which tie it
 * to the label and mark it invalid for assistive technology. `hint` is required, so a field cannot be written without saying what it
 * is for. `action` is a button on the label row, right-aligned; `labelHidden` keeps the label for assistive technology where a
 * heading already says it and carries the help; fields side by side that should line up pass `action` too, `null` when they have no button, so the row is as tall as the button's.
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
    action?: ReactNode;
    errors: string[];
    children: (props: object) => ReactNode;
} & ({ hint: string; labelHidden?: false } | { labelHidden: true; hint?: undefined })) {
    const id = `${prefix}-${name}`;
    const heading =
        labelHidden === true ? (
            <Label htmlFor={id} className="sr-only">
                {label}
            </Label>
        ) : (
            <div className="flex min-w-0 items-center gap-1">
                <Label htmlFor={id}>{label}</Label>
                <InfoTip about={label} help={hint} />
            </div>
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

/** The heading of a group of controls, with its help. */
export function Legend({ hint, children }: { hint: string; children: string }) {
    return (
        <legend className="flex items-center gap-1 text-sm font-medium">
            {children}
            <InfoTip about={children} help={hint} />
        </legend>
    );
}
