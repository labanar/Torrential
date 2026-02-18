import { Input } from "@/components/ui/input";
import { Control, Controller } from "react-hook-form";

interface FormNumericInputProps {
  min?: number;
  max?: number;
  className?: string;
  fieldName: string;
  control: Control<any, any>;
}

export const FormNumericInput: React.FC<FormNumericInputProps> = ({
  min,
  max,
  className,
  fieldName,
  control,
}) => {
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
