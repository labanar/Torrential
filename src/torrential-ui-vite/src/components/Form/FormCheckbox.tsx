import { Checkbox } from "@/components/ui/checkbox";
import { Control, Controller, FieldValues, Path } from "react-hook-form";

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
          className={className}
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: "0.5rem",
            minHeight: "var(--touch-target-min)",
            cursor: "pointer",
            lineHeight: 1.35,
          }}
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
