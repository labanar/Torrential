import { Checkbox } from "@/components/ui/checkbox";
import { Control, Controller, FieldValues, Path } from "react-hook-form";
import { cn } from "@/lib/utils";

interface FormCheckboxProps<TFieldValues extends FieldValues> {
  className?: string;
  fieldName: Path<TFieldValues>;
  text?: string;
  control: Control<TFieldValues>;
}

export const FormCheckbox = <TFieldValues extends FieldValues>({
  className,
  fieldName,
  control,
  text,
}: FormCheckboxProps<TFieldValues>) => {
  return (
    <Controller
      name={fieldName}
      control={control}
      render={({ field: { onChange, onBlur, value, ref } }) => (
        <label
          className={cn(
            "inline-flex min-h-11 cursor-pointer items-center gap-2 leading-[1.35]",
            className
          )}
        >
          <Checkbox
            checked={Boolean(value)}
            onCheckedChange={(checked) => onChange(checked === true)}
            onBlur={onBlur}
            ref={ref}
          />
          {text ?? ""}
        </label>
      )}
    />
  );
};
