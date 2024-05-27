import { Input } from "@chakra-ui/react";
import { Control, Controller } from "react-hook-form";

interface FormInputProps {
  className?: string;
  fieldName: string;
  control: Control<any, any>;
}

export const FormInput: React.FC<FormInputProps> = ({
  className,
  fieldName,
  control,
}) => {
  return (
    <Controller
      name={fieldName}
      control={control}
      render={({ field }) => <Input {...field} />}
    />
  );
};
