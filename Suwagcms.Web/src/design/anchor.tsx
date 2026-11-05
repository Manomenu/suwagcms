import * as React from "react";
import { Link } from "wouter";
import { cn } from "@/design/classNames";

export function Anchor({ className, ...props }: React.ComponentProps<typeof Link>) {
    return <Link className={cn("text-anchor underline-offset-4 hover:underline", className)} {...props} />;
}
