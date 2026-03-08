import {
  IconDefinition,
  faGear,
  faPeopleGroup,
  faPlug,
  faUpDown,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useEffect, useMemo, useRef, useState } from "react";
import { useHotkeys, useHotkeysContext } from "react-hotkeys-hook";
import { NavigateFunction, useNavigate } from "react-router-dom";
import { AppDispatch, useAppDispatch } from "../../store";
import { AlfredContext } from "../../store/slices/alfredSlice";
import { Dialog, DialogContent } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Separator } from "@/components/ui/separator";
import { ScrollArea } from "@/components/ui/scroll-area";
import { cn } from "@/lib/utils";

export default function Alfred() {
  const { enableScope, disableScope, enabledScopes } = useHotkeysContext();

  const navigate = useNavigate();
  const dispatch = useAppDispatch();

  const [isOpen, setIsOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [selectedId, setSelectedId] = useState(0);
  const [scopesToEnableOnClose, setScopesToEnableOnClose] = useState<string[]>([]);
  const inputRef = useRef<HTMLInputElement>(null);

  const filteredSuggestions = useMemo(() => {
    const trimmed = query.trim().toLowerCase();
    if (!trimmed) {
      return globalSuggestions;
    }

    return globalSuggestions.filter((command) => {
      const target = `${command.group} ${command.title}`.toLowerCase();
      return target.includes(trimmed);
    });
  }, [query]);

  const groupedSuggestions = useMemo(() => {
    return filteredSuggestions.reduce<Record<string, SearchSuggestion[]>>((acc, item) => {
      const group = item.group;
      acc[group] ??= [];
      acc[group].push(item);
      return acc;
    }, {});
  }, [filteredSuggestions]);

  useEffect(() => {
    if (selectedId >= filteredSuggestions.length) {
      setSelectedId(0);
    }
  }, [filteredSuggestions.length, selectedId]);

  useHotkeys(
    "mod+k",
    (event) => {
      event.preventDefault();
      setIsOpen((prev) => !prev);
    },
    {
      scopes: ["global"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "mod+ ",
    (event) => {
      event.preventDefault();
      setIsOpen((prev) => !prev);
    },
    {
      scopes: ["global"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useEffect(() => {
    if (isOpen) {
      setScopesToEnableOnClose(enabledScopes);
      enabledScopes.forEach((scope) => disableScope(scope));
      enableScope("search");
      window.setTimeout(() => inputRef.current?.focus(), 0);
      return;
    }

    disableScope("search");
    scopesToEnableOnClose.forEach((scope) => {
      enableScope(scope);
    });
    enableScope("global");
    setSelectedId(0);
    setQuery("");
  }, [disableScope, enableScope, enabledScopes, isOpen, scopesToEnableOnClose]);

  useHotkeys(
    "up",
    (event) => {
      event.preventDefault();
      if (filteredSuggestions.length === 0) {
        return;
      }

      let nextId = selectedId - 1;
      if (nextId < 0) {
        nextId = filteredSuggestions.length - 1;
      }
      setSelectedId(nextId);
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "down",
    (event) => {
      event.preventDefault();
      if (filteredSuggestions.length === 0) {
        return;
      }

      let nextId = selectedId + 1;
      if (nextId > filteredSuggestions.length - 1) {
        nextId = 0;
      }
      setSelectedId(nextId);
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "enter",
    (event) => {
      event.preventDefault();
      if (selectedId >= 0 && filteredSuggestions.length > selectedId) {
        const suggestion = filteredSuggestions[selectedId];
        suggestion.action({ dispatch, navigate });
        setIsOpen(false);
      }
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "esc",
    (event) => {
      event.preventDefault();
      setIsOpen(false);
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  let runningIndex = -1;

  return (
    <Dialog open={isOpen} onOpenChange={setIsOpen}>
      <DialogContent className="max-w-2xl gap-0 overflow-hidden p-0">
        <div className="p-3">
          <Input
            ref={inputRef}
            placeholder="Search commands..."
            value={query}
            aria-label="Search commands"
            onChange={(event) => {
              setQuery(event.target.value);
              setSelectedId(0);
            }}
          />
        </div>
        <Separator />
        <ScrollArea className="max-h-[min(65vh,28rem)]">
          {filteredSuggestions.length === 0 && (
            <div className="p-6 text-sm text-muted-foreground">No commands found.</div>
          )}
          {Object.entries(groupedSuggestions).map(([group, items]) => (
            <div key={group} className="p-2">
              <p className="px-2 pb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                {group}
              </p>
              <div role="listbox" aria-label={`${group} commands`}>
                {items.map((item) => {
                  runningIndex += 1;
                  const itemIndex = runningIndex;
                  const isSelected = selectedId === itemIndex;

                  return (
                    <button
                      key={item.title}
                      type="button"
                      role="option"
                      aria-selected={isSelected}
                      className={cn(
                        "flex w-full items-center gap-3 rounded-md px-3 py-2 text-left text-sm transition-colors",
                        isSelected ? "bg-accent text-accent-foreground" : "hover:bg-muted"
                      )}
                      onMouseEnter={() => setSelectedId(itemIndex)}
                      onClick={() => {
                        item.action({ dispatch, navigate });
                        setIsOpen(false);
                      }}
                    >
                      <span className="inline-flex h-7 w-7 items-center justify-center rounded-md border bg-background text-muted-foreground">
                        <FontAwesomeIcon icon={item.icon} />
                      </span>
                      <span>{item.title}</span>
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}

interface SearchSuggestion {
  context: AlfredContext;
  group: string;
  icon: IconDefinition;
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
    group: "Navigate",
    icon: faUpDown,
    title: "Torrents",
    action: ({ navigate }) => navigate("/"),
  },
  {
    context: AlfredContext.Global,
    group: "Navigate",
    icon: faPeopleGroup,
    title: "Peers",
    action: ({ navigate }) => navigate("/peers"),
  },
  {
    context: AlfredContext.Global,
    group: "Navigate",
    icon: faPlug,
    title: "Integrations",
    action: ({ navigate }) => navigate("/integrations"),
  },
  {
    context: AlfredContext.Global,
    group: "Navigate",
    icon: faGear,
    title: "Settings",
    action: ({ navigate }) => navigate("/settings"),
  },
];
