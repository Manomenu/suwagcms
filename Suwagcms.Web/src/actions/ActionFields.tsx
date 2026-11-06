import type { ReactNode } from "react";

import type { Mistake } from "@/api/client";
import { Button } from "@/design/button";
import { ConstraintFields } from "@/design/constraintFields";
import { TYPE_HINT } from "@/design/factHints";
import { Field, Legend } from "@/design/field";
import { Input } from "@/design/input";
import { Select } from "@/design/select";

import { emptyRow, METHODS, placeOf, type ActionForm, type FieldRow, type HeaderRow, type ListName } from "./actionForm";

const HTTP = "implementation.http";

const HINTS = {
    id: "The action's id: how a step of a procedure names it. One camelCase word, like checkServiceability. To change it later, use Change id.",
    description: "What this action does, in a sentence, so an author choosing one for a step knows which it is.",
    method: "How the bot calls the address. Anything but GET may change something, so a test run asks first.",
    url: "The absolute address the bot calls. Each {name} in it is filled from the parameter of that name.",
    timeout: "How many seconds the bot waits for an answer before the call counts as failed: 1 to 60; 10 when left empty.",
    headers: "Sent with every call, like a token the other system asks for.",
    headerName: "The name of the HTTP header, like Authorization.",
    headerValue: "What the header says. Write a secret as {secret:CRM_TOKEN}, never its value.",
    parameters: "What the action takes. A step of a procedure fills each parameter from a fact, so the action itself never names a fact.",
    results: "What the action gives back, from the top level of the answer. A step of a procedure writes each result to a fact.",
    fieldName: "How the step mapping it and the address refer to it.",
    fieldDescription: "What this value is, so an author mapping it to a fact picks the right one.",
} as const;

const TYPES = ["text", "enum", "int"] as const;

/** One field with the mistakes its place names written beneath it. */
function Placed({
    place,
    label,
    hint,
    mistakes,
    children,
}: {
    place: string;
    label: string;
    hint: string;
    mistakes: Mistake[];
    children: (props: object) => ReactNode;
}) {
    return (
        <Field
            prefix="action"
            name={place}
            label={label}
            hint={hint}
            errors={mistakes.filter((mistake) => placeOf(mistake) === place).map((mistake) => `${mistake.message} ${mistake.fix}`)}
        >
            {children}
        </Field>
    );
}

function Constraint({
    row,
    at,
    mistakes,
    onChange,
}: {
    row: FieldRow;
    at: string;
    mistakes: Mistake[];
    onChange: (change: Partial<FieldRow>) => void;
}) {
    return (
        <div className={row.type === "int" ? "flex gap-2" : "contents"}>
            <ConstraintFields
                form={row}
                // Why: a row's cell is too narrow to say "separated by commas" in the label, and every label of a row must stay on one line to line up.
                field={(name, label, hint, control) => (
                    <Placed place={`${at}.${name}`} label={name === "values" ? "Values" : label} hint={hint} mistakes={mistakes}>
                        {control}
                    </Placed>
                )}
                onChange={onChange}
            />
        </div>
    );
}

function Rows({
    list,
    title,
    hint,
    rows,
    mistakes,
    onChange,
}: {
    list: ListName;
    title: string;
    hint: string;
    rows: FieldRow[];
    mistakes: Mistake[];
    onChange: (rows: FieldRow[]) => void;
}) {
    const noun = list === "parameters" ? "parameter" : "result";
    return (
        <fieldset className="flex flex-col gap-2" aria-label={title}>
            <Legend hint={hint}>{title}</Legend>
            {rows.map((row, index) => {
                const at = `${list}.${row.name}`;
                const change = (part: Partial<FieldRow>) => {
                    onChange(rows.map((other, position) => (position === index ? { ...other, ...part } : other)));
                };
                return (
                    <div key={index} className="grid grid-cols-[1fr_6rem_1fr_2fr_auto] items-start gap-2" data-row={`${noun}-${index}`}>
                        <Placed place={at} label="Name" hint={HINTS.fieldName} mistakes={mistakes}>
                            {(props) => (
                                <Input
                                    {...props}
                                    value={row.name}
                                    onChange={(event) => {
                                        change({ name: event.target.value });
                                    }}
                                />
                            )}
                        </Placed>
                        <Placed place={`${at}.type`} label="Type" hint={TYPE_HINT} mistakes={mistakes}>
                            {(props) => (
                                <Select
                                    {...props}
                                    value={row.type}
                                    onChange={(event) => {
                                        change({ type: event.target.value });
                                    }}
                                >
                                    {TYPES.map((type) => (
                                        <option key={type} value={type}>
                                            {type}
                                        </option>
                                    ))}
                                </Select>
                            )}
                        </Placed>
                        <Constraint row={row} at={at} mistakes={mistakes} onChange={change} />
                        <Placed place={`${at}.description`} label="Description" hint={HINTS.fieldDescription} mistakes={mistakes}>
                            {(props) => (
                                <Input
                                    {...props}
                                    value={row.description}
                                    onChange={(event) => {
                                        change({ description: event.target.value });
                                    }}
                                />
                            )}
                        </Placed>
                        <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className="mt-6"
                            aria-label={`Remove ${noun} ${row.name}`}
                            onClick={() => {
                                onChange(rows.filter((_, position) => position !== index));
                            }}
                        >
                            ✕
                        </Button>
                    </div>
                );
            })}
            <div>
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => {
                        onChange([...rows, emptyRow()]);
                    }}
                >
                    Add a {noun}
                </Button>
            </div>
        </fieldset>
    );
}

function Headers({ headers, mistakes, onChange }: { headers: HeaderRow[]; mistakes: Mistake[]; onChange: (headers: HeaderRow[]) => void }) {
    return (
        <fieldset className="flex flex-col gap-2" aria-label="Headers">
            <Legend hint={HINTS.headers}>Headers</Legend>
            <p className="text-sm text-muted-foreground">
                A secret is a name found in the bot's environment, written as {"{secret:CRM_TOKEN}"}. The CMS never holds its value.
            </p>
            {headers.map((header, index) => {
                const change = (part: Partial<HeaderRow>) => {
                    onChange(headers.map((other, position) => (position === index ? { ...other, ...part } : other)));
                };
                return (
                    <div key={index} className="grid grid-cols-[1fr_2fr_auto] items-start gap-2">
                        <Field prefix="action-header-name" name={String(index)} label="Header" hint={HINTS.headerName} errors={[]}>
                            {(props) => (
                                <Input
                                    {...props}
                                    value={header.name}
                                    onChange={(event) => {
                                        change({ name: event.target.value });
                                    }}
                                />
                            )}
                        </Field>
                        <Placed place={`${HTTP}.headers.${header.name}`} label="Value" hint={HINTS.headerValue} mistakes={mistakes}>
                            {(props) => (
                                <Input
                                    {...props}
                                    value={header.value}
                                    onChange={(event) => {
                                        change({ value: event.target.value });
                                    }}
                                />
                            )}
                        </Placed>
                        <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className="mt-6"
                            aria-label={`Remove header ${header.name}`}
                            onClick={() => {
                                onChange(headers.filter((_, position) => position !== index));
                            }}
                        >
                            ✕
                        </Button>
                    </div>
                );
            })}
            <div>
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => {
                        onChange([...headers, { name: "", value: "" }]);
                    }}
                >
                    Add a header
                </Button>
            </div>
        </fieldset>
    );
}

/** The fields of the action form; a mistake shows beside the field its path names. */
export function ActionFields({
    form,
    mistakes,
    idLocked,
    onChange,
}: {
    form: ActionForm;
    mistakes: Mistake[];
    idLocked: boolean;
    onChange: (change: Partial<ActionForm>) => void;
}) {
    return (
        <>
            <Placed place="id" label="Id" hint={HINTS.id} mistakes={mistakes}>
                {(props) => (
                    <Input
                        {...props}
                        value={form.id}
                        disabled={idLocked}
                        onChange={(event) => {
                            onChange({ id: event.target.value });
                        }}
                    />
                )}
            </Placed>
            <Placed place="description" label="Description" hint={HINTS.description} mistakes={mistakes}>
                {(props) => (
                    <Input
                        {...props}
                        value={form.description}
                        onChange={(event) => {
                            onChange({ description: event.target.value });
                        }}
                    />
                )}
            </Placed>
            <div className="grid grid-cols-[7rem_1fr_7rem] items-start gap-2">
                <Placed place={`${HTTP}.method`} label="Method" hint={HINTS.method} mistakes={mistakes}>
                    {(props) => (
                        <Select
                            {...props}
                            value={form.method}
                            onChange={(event) => {
                                onChange({ method: event.target.value });
                            }}
                        >
                            {METHODS.map((method) => (
                                <option key={method} value={method}>
                                    {method}
                                </option>
                            ))}
                        </Select>
                    )}
                </Placed>
                <Placed place={`${HTTP}.url`} label="URL" hint={HINTS.url} mistakes={mistakes}>
                    {(props) => (
                        <Input
                            {...props}
                            value={form.url}
                            onChange={(event) => {
                                onChange({ url: event.target.value });
                            }}
                        />
                    )}
                </Placed>
                <Placed place={`${HTTP}.timeoutSeconds`} label="Timeout (s)" hint={HINTS.timeout} mistakes={mistakes}>
                    {(props) => (
                        <Input
                            {...props}
                            inputMode="numeric"
                            value={form.timeout}
                            onChange={(event) => {
                                onChange({ timeout: event.target.value });
                            }}
                        />
                    )}
                </Placed>
            </div>
            <Headers
                headers={form.headers}
                mistakes={mistakes}
                onChange={(headers) => {
                    onChange({ headers });
                }}
            />
            <Rows
                list="parameters"
                title="Parameters"
                hint={HINTS.parameters}
                rows={form.parameters}
                mistakes={mistakes}
                onChange={(parameters) => {
                    onChange({ parameters });
                }}
            />
            <Rows
                list="results"
                title="Results"
                hint={HINTS.results}
                rows={form.results}
                mistakes={mistakes}
                onChange={(results) => {
                    onChange({ results });
                }}
            />
        </>
    );
}
