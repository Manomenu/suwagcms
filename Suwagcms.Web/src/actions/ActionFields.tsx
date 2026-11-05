import type { ReactNode } from "react";

import type { Mistake } from "@/api/client";
import { Button } from "@/design/button";
import { Field } from "@/design/field";
import { Input } from "@/design/input";
import { Select } from "@/design/select";

import { emptyRow, METHODS, placeOf, type ActionForm, type FieldRow, type HeaderRow, type ListName } from "./actionForm";

const HTTP = "implementation.http";

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
    hint?: string;
    mistakes: Mistake[];
    children: (props: object) => ReactNode;
}) {
    return (
        <Field
            prefix="action"
            name={place}
            label={label}
            {...(hint === undefined ? {} : { hint })}
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
    if (row.type === "enum") {
        return (
            <Placed place={`${at}.values`} label="Values" hint="Separated by commas." mistakes={mistakes}>
                {(props) => (
                    <Input
                        {...props}
                        value={row.values}
                        onChange={(event) => {
                            onChange({ values: event.target.value });
                        }}
                    />
                )}
            </Placed>
        );
    }
    if (row.type === "int") {
        return (
            <div className="flex gap-2">
                <Placed place={`${at}.min`} label="Min" mistakes={mistakes}>
                    {(props) => (
                        <Input
                            {...props}
                            value={row.min}
                            onChange={(event) => {
                                onChange({ min: event.target.value });
                            }}
                        />
                    )}
                </Placed>
                <Placed place={`${at}.max`} label="Max" mistakes={mistakes}>
                    {(props) => (
                        <Input
                            {...props}
                            value={row.max}
                            onChange={(event) => {
                                onChange({ max: event.target.value });
                            }}
                        />
                    )}
                </Placed>
            </div>
        );
    }
    return (
        <Placed place={`${at}.maxLength`} label="Maximum length" mistakes={mistakes}>
            {(props) => (
                <Input
                    {...props}
                    value={row.maxLength}
                    onChange={(event) => {
                        onChange({ maxLength: event.target.value });
                    }}
                />
            )}
        </Placed>
    );
}

function Rows({
    list,
    title,
    rows,
    mistakes,
    onChange,
}: {
    list: ListName;
    title: string;
    rows: FieldRow[];
    mistakes: Mistake[];
    onChange: (rows: FieldRow[]) => void;
}) {
    const noun = list === "parameters" ? "parameter" : "result";
    return (
        <fieldset className="flex flex-col gap-2" aria-label={title}>
            <legend className="text-sm font-medium">{title}</legend>
            {rows.map((row, index) => {
                const at = `${list}.${row.name}`;
                const change = (part: Partial<FieldRow>) => {
                    onChange(rows.map((other, position) => (position === index ? { ...other, ...part } : other)));
                };
                return (
                    <div key={index} className="grid grid-cols-[1fr_6rem_1fr_2fr_auto] items-start gap-2" data-row={`${noun}-${index}`}>
                        <Placed place={at} label="Name" mistakes={mistakes}>
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
                        <Placed place={`${at}.type`} label="Type" mistakes={mistakes}>
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
                        <Placed place={`${at}.description`} label="Description" mistakes={mistakes}>
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
            <legend className="text-sm font-medium">Headers</legend>
            <p className="text-sm text-muted-foreground">
                A secret is a name found in the bot's environment, written as {"{secret:CRM_TOKEN}"}. The CMS never holds its value.
            </p>
            {headers.map((header, index) => {
                const change = (part: Partial<HeaderRow>) => {
                    onChange(headers.map((other, position) => (position === index ? { ...other, ...part } : other)));
                };
                return (
                    <div key={index} className="grid grid-cols-[1fr_2fr_auto] items-start gap-2">
                        <Field prefix="action-header-name" name={String(index)} label="Header" errors={[]}>
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
                        <Placed place={`${HTTP}.headers.${header.name}`} label="Value" mistakes={mistakes}>
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
            <Placed place="id" label="Id" hint="One camelCase word, like checkServiceability." mistakes={mistakes}>
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
            <Placed place="description" label="Description" mistakes={mistakes}>
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
                <Placed place={`${HTTP}.method`} label="Method" mistakes={mistakes}>
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
                <Placed place={`${HTTP}.url`} label="URL" hint="An absolute address; each {name} in it is a parameter." mistakes={mistakes}>
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
                <Placed place={`${HTTP}.timeoutSeconds`} label="Timeout (s)" hint="1 to 60; 10 when left empty." mistakes={mistakes}>
                    {(props) => (
                        <Input
                            {...props}
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
                rows={form.parameters}
                mistakes={mistakes}
                onChange={(parameters) => {
                    onChange({ parameters });
                }}
            />
            <Rows
                list="results"
                title="Results"
                rows={form.results}
                mistakes={mistakes}
                onChange={(results) => {
                    onChange({ results });
                }}
            />
        </>
    );
}
