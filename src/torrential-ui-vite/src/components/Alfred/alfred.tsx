import {
  IconDefinition,
  faGear,
  faPeopleGroup,
  faPlug,
  faUpDown,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useEffect, useRef, useState } from "react";
import { useHotkeys, useHotkeysContext } from "react-hotkeys-hook";
import styles from "./alfred.module.css";
import { NavigateFunction, useNavigate } from "react-router-dom";
import { AppDispatch, useAppDispatch } from "../../store";
import { AlfredContext } from "../../store/slices/alfredSlice";
import classNames from "classnames";
import { Dialog, DialogContent } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Separator } from "@/components/ui/separator";

export default function Alfred() {
  const { enableScope, disableScope, enabledScopes } = useHotkeysContext();

  const navigate = useNavigate();
  const dispatch = useAppDispatch();

  const [isOpen, setSearchOpen] = useState(false);
  const [selectedId, setSelectedId] = useState(0);
  const [suggestions] = useState(globalSuggestions);
  const [scopesToEnableOnClose, setScopesToEnableOnClose] = useState<string[]>(
    []
  );
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setSelectedId(0);
  }, [suggestions]);

  const onToggle = () => {
    setSearchOpen((prevOpen) => !prevOpen);
  };

  useHotkeys(
    "mod+ ",
    () => {
      onToggle();
    },
    {
      scopes: ["global"],
      enableOnFormTags: ["input", "textarea", "select"],
    },
    [onToggle]
  );

  useEffect(() => {
    if (isOpen) {
      setScopesToEnableOnClose(enabledScopes);
      enabledScopes.forEach((s) => disableScope(s));
      enableScope("search");
      window.setTimeout(() => inputRef.current?.focus(), 0);
    } else {
      disableScope("search");
      scopesToEnableOnClose.forEach((s) => {
        enableScope(s);
      });
      enableScope("global");
      setSelectedId(0);
    }
    return () => disableScope("search");
  }, [isOpen]);

  useHotkeys(
    "up",
    () => {
      let nextId = selectedId - 1;
      if (nextId < 0) nextId = suggestions.length - 1;
      setSelectedId(nextId);
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "down",
    () => {
      let nextId = selectedId + 1;
      if (nextId > suggestions.length - 1) nextId = nextId - suggestions.length;
      setSelectedId(nextId);
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "enter",
    () => {
      if (selectedId >= 0 && suggestions.length > selectedId) {
        const suggestion = suggestions[selectedId];
        suggestion.action({ dispatch, navigate });
        setSearchOpen(false);
      }
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "esc",
    () => {
      setSearchOpen(false);
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  return (
    <Dialog open={isOpen} onOpenChange={setSearchOpen}>
      <DialogContent className={styles.dialogContent}>
        <div className={styles.inputContainer}>
          <Input
            ref={inputRef}
            placeholder="Type to search"
            className={styles.searchInput}
            onKeyDown={(e) => {
              if (e.key === "Escape") {
                e.preventDefault();
              }
            }}
          />
        </div>
        <Separator />

        <div className={styles.modalBody}>
          {suggestions.map((s, i) => (
            <SearchSuggestion
              key={s.title}
              selected={selectedId === i}
              icon={s.icon}
              title={s.title}
            />
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}

interface SearchSuggestionProps {
  selected: boolean;
  icon?: IconDefinition;
  title: string;
}

function SearchSuggestion({ selected, icon, title }: SearchSuggestionProps) {
  return (
    <div
      className={classNames(styles.suggestion, {
        [styles.selected]: selected,
      })}
    >
      <FontAwesomeIcon icon={icon!} size="xl" width={"28px"} />
      <span className={styles.suggestionLabel}>{title}</span>
    </div>
  );
}

interface SearchSuggestion {
  context: AlfredContext;
  icon?: IconDefinition;
  title: string;
  action: (actionProps: SearchSuggestionActionProps) => void;
}

interface SearchSuggestionActionProps {
  dispatch: AppDispatch;
  navigate: NavigateFunction;
}

const globalSuggestions: SearchSuggestion[] = [
  {
    context: AlfredContext.Global,
    icon: faUpDown,
    title: "Torrents",
    action: ({ navigate }) => navigate("/"),
  },
  {
    context: AlfredContext.Global,
    icon: faPeopleGroup,
    title: "Peers",
    action: ({ navigate }) => navigate("/peers"),
  },
  {
    context: AlfredContext.Global,
    icon: faPlug,
    title: "Integrations",
    action: ({ navigate }) => navigate("/integrations"),
  },
  {
    context: AlfredContext.Global,
    icon: faGear,
    title: "Settings",
    action: ({ navigate }) => navigate("/settings"),
  },
];
