import type { ReactNode } from "react";

import type { Mistake } from "@/api/client";
import { Input } from "@/design/input";
import { Label } from "@/design/label";

import { fieldOf, type FactForm, type FieldName } from "./factForm";

const SELECT = "h-9 w-full rounded-md border border-input bg-background px-2 text-sm";

/** One field with the mistakes its path names written beneath it; the field is marked invalid for assistive technology. */
function Field({
    field,
    label,
    mistakes,
    children,
}: {
    field: FieldName;
    label: string;
    mistakes: Mistake[];
    children: (props: object) => ReactNode;
}) {
    const own = mistakes.filter((mistake) => fieldOf(mistake) === field);
    return (
        <div data-field={field} className="flex flex-col gap-2">
            <Label htmlFor={`fact-${field}`}>{label}</Label>
            {children({
                id: `fact-${field}`,
                "aria-invalid": own.length > 0 || undefined,
                "aria-describedby": own.length > 0 ? `fact-${field}-mistakes` : undefined,
            })}
            {own.length > 0 && (
                <ul id={`fact-${field}-mistakes`} className="list-disc pl-4 text-sm text-destructive">
                    {own.map((mistake) => (
                        <li key={`${mistake.code}:${mistake.path}`}>
                            {mistake.message} {mistake.fix}
                        </li>
                    ))}
                </ul>
            )}
        </div>
    );
}

/** The fields of the fact form; a mistake shows beside the field its path names. */
export function FactFields({
    form,
    mistakes,
    nameLocked,
    onChange,
}: {
    form: FactForm;
    mistakes: Mistake[];
    nameLocked: boolean;
    onChange: (change: Partial<FactForm>) => void;
}) {
    return (
        <>
            <Field field="name" label="Name" mistakes={mistakes}>
                {(props) => (
                    <Input
                        {...props}
                        value={form.name}
                        disabled={nameLocked}
                        placeholder="customer.city"
                        onChange={(event) => {
                            onChange({ name: event.currentTarget.value });
                        }}
                    />
                )}
            </Field>
            <Field field="type" label="Type" mistakes={mistakes}>
                {(props) => (
                    <select
                        {...props}
                        className={SELECT}
                        value={form.type}
                        onChange={(event) => {
                            onChange({ type: event.currentTarget.value });
                        }}
                    >
                        <option value="text">text</option>
                        <option value="enum">enum</option>
                        <option value="int">int</option>
                    </select>
                )}
            </Field>
            {form.type === "enum" && (
                <Field field="values" label="Values (separated by commas)" mistakes={mistakes}>
                    {(props) => (
                        <Input
                            {...props}
                            value={form.values}
                            placeholder="house, flat"
                            onChange={(event) => {
                                onChange({ values: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
            )}
            {form.type === "int" && (
                <>
                    <Field field="min" label="Minimum" mistakes={mistakes}>
                        {(props) => (
                            <Input
                                {...props}
                                inputMode="numeric"
                                value={form.min}
                                onChange={(event) => {
                                    onChange({ min: event.currentTarget.value });
                                }}
                            />
                        )}
                    </Field>
                    <Field field="max" label="Maximum" mistakes={mistakes}>
                        {(props) => (
                            <Input
                                {...props}
                                inputMode="numeric"
                                value={form.max}
                                onChange={(event) => {
                                    onChange({ max: event.currentTarget.value });
                                }}
                            />
                        )}
                    </Field>
                </>
            )}
            {form.type === "text" && (
                <Field field="maxLength" label="Maximum length" mistakes={mistakes}>
                    {(props) => (
                        <Input
                            {...props}
                            inputMode="numeric"
                            value={form.maxLength}
                            onChange={(event) => {
                                onChange({ maxLength: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
            )}
            <Field field="description" label="Description" mistakes={mistakes}>
                {(props) => (
                    <Input
                        {...props}
                        value={form.description}
                        placeholder="The city the customer lives in"
                        onChange={(event) => {
                            onChange({ description: event.currentTarget.value });
                        }}
                    />
                )}
            </Field>
            <Field field="writableBy" label="Writable by" mistakes={mistakes}>
                {(props) => (
                    <select
                        {...props}
                        className={SELECT}
                        value={form.writableBy}
                        onChange={(event) => {
                            onChange({ writableBy: event.currentTarget.value });
                        }}
                    >
                        <option value="assign">assign</option>
                        <option value="set">set</option>
                        <option value="correct">correct</option>
                    </select>
                )}
            </Field>
        </>
    );
}
