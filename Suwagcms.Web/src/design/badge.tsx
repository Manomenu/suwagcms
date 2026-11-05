import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/design/classNames";

const badgeVariants = cva("inline-flex items-center rounded-full px-2 py-0.5 text-xs font-bold tracking-wide whitespace-nowrap uppercase", {
    variants: {
        variant: {
            neutral: "bg-secondary text-muted-foreground",
            primary: "bg-primary-light text-primary-light-foreground",
            success: "bg-success-light text-success",
            warning: "bg-warning-light text-warning",
            danger: "bg-destructive-light text-destructive",
        },
    },
    defaultVariants: { variant: "neutral" },
});

export function Badge({ className, variant, ...props }: React.ComponentProps<"span"> & VariantProps<typeof badgeVariants>) {
    return <span data-slot="badge" className={cn(badgeVariants({ variant }), className)} {...props} />;
}
