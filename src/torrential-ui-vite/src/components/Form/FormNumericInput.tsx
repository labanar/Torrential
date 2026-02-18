import { Input } from "@/components/ui/input";
import { Control, Controller, FieldValues, Path } from "react-hook-form";

interface FormNumericInputProps<TFieldValues extends FieldValues> {
  min?: number;
  max?: number;
  className?: string;
  fieldName: Path<TFieldValues>;
  control: Control<TFieldValues>;
}

export const FormNumericInput = <TFieldValues extends FieldValues>({
  min,
  max,
  className,
  fieldName,
  control,
}: FormNumericInputProps<TFieldValues>) => {
  return (
    <Controller
      name={fieldName}
      control={control}
      render={({ field }) => (
        <Input
          {...field}
          type="number"
          min={min}
          max={max}
          className={className}
          value={field.value ?? ""}
          onChange={(event) => field.onChange(event.target.value)}
        />
      )}
    />
  );
};
