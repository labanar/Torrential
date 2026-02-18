import { Checkbox } from "@/components/ui/checkbox";
import { Control, Controller } from "react-hook-form";

interface FormCheckboxProps {
  className?: string;
  fieldName: string;
  text?: string;
  control: Control<any, any>;
}

export const FormCheckbox: React.FC<FormCheckboxProps> = ({
  className,
  fieldName,
  control,
  text,
}) => {
  return (
    <Controller
      name={fieldName}
      control={control}
      render={({ field: { onChange, onBlur, value, ref } }) => (
        <label
          className={className}
          style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem" }}
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
