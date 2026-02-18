import type * as React from "react";
import { Toaster as Sonner } from "sonner";

type ToasterProps = React.ComponentProps<typeof Sonner>;

const Toaster = ({ ...props }: ToasterProps) => {
  return (
    <Sonner
      closeButton
      richColors
      position="bottom-right"
      offset="1rem"
      mobileOffset="0.75rem"
      {...props}
    />
  );
};

export { Toaster };
