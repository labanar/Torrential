import { Checkbox } from "@chakra-ui/react";
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
        <Checkbox
          onChange={(e) => onChange(e.target.checked)}
          onBlur={onBlur}
          isChecked={value}
          ref={ref}
          className={className}
        >
          {text ?? ""}
        </Checkbox>
      )}
    />
  );
};
