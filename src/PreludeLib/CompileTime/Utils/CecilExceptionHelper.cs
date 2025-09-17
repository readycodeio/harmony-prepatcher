using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PreludeLib.CompileTime.Utils;

public class CecilExceptionHelper
{
    private class LabelledExceptionHandler
    {
        public Instruction? TryStart;
        public Instruction? TryEnd;
        public Instruction? HandlerStart;
        public Instruction? HandlerEnd;
        public Instruction? FilterStart;
        public ExceptionHandlerType HandlerType;
        public TypeReference? ExceptionType;
    }
    
    private class ExceptionHandlerChain
    {
        private readonly CecilExceptionHelper _owner;
        private readonly ILProcessor _il;

        private readonly Instruction _Start;
        public readonly Instruction SkipAll;
        private Instruction _SkipHandler;

        private LabelledExceptionHandler? _Prev;
        private LabelledExceptionHandler? _Handler;

        public ExceptionHandlerChain(CecilExceptionHelper owner)
        {
            _owner = owner;
            _il = owner._il;

            _Start = Instruction.Create(OpCodes.Nop);
            _il.Append(_Start);

            SkipAll = Instruction.Create(OpCodes.Nop);
        }

        public LabelledExceptionHandler BeginHandler(ExceptionHandlerType type)
        {
            var prev = _Prev = _Handler;
            if (prev is not null)
                EndHandler(prev);

            _il.Emit(OpCodes.Leave, _SkipHandler = Instruction.Create(OpCodes.Nop));

            var handlerStart = Instruction.Create(OpCodes.Nop);
            _il.Append(handlerStart);

            var next = _Handler = new LabelledExceptionHandler
            {
                TryStart = _Start,
                TryEnd = handlerStart,
                HandlerType = type,
                HandlerEnd = _SkipHandler
            };
            if (type == ExceptionHandlerType.Filter)
                next.FilterStart = handlerStart;
            else
                next.HandlerStart = handlerStart;

            return next;
        }

        public void EndHandler(LabelledExceptionHandler handler)
        {
            var skip = _SkipHandler;

            switch (handler.HandlerType)
            {
                case ExceptionHandlerType.Filter:
                    _il.Emit(OpCodes.Endfilter);
                    break;

                case ExceptionHandlerType.Finally:
                    _il.Emit(OpCodes.Endfinally);
                    break;

                default:
                    _il.Emit(OpCodes.Leave, skip);
                    break;
            }

            _il.Append(skip);
            
            _il.Body.ExceptionHandlers.Add(new ExceptionHandler(handler.HandlerType)
            {
                TryStart = handler.TryStart!,
                TryEnd = handler.TryEnd!,
                HandlerStart = handler.HandlerStart!,
                HandlerEnd = handler.HandlerEnd!,
                FilterStart = handler.FilterStart!,
                CatchType = handler.ExceptionType!
            });
        }

        public void End()
        {
            EndHandler(_Handler ?? throw new InvalidOperationException("Cannot end when there is no current handler!"));
            _il.Append(SkipAll);
        }
    }

    private ILProcessor _il;
    private readonly Stack<ExceptionHandlerChain> _ExceptionHandlers = new Stack<ExceptionHandlerChain>();

    public CecilExceptionHelper(ILProcessor il)
    {
        _il = il;
    }
    
    public Instruction BeginExceptionBlock()
    {
        var chain = new ExceptionHandlerChain(this);
        _ExceptionHandlers.Push(chain);
        return chain.SkipAll;
    }

    public void BeginCatchBlock(TypeReference exceptionType)
    {
        var handler = _ExceptionHandlers.Peek().BeginHandler(ExceptionHandlerType.Catch);
        handler.ExceptionType = exceptionType;
    }

    public void BeginExceptFilterBlock()
    {
        _ExceptionHandlers.Peek().BeginHandler(ExceptionHandlerType.Filter);
    }

    public void BeginFaultBlock()
    {
        _ExceptionHandlers.Peek().BeginHandler(ExceptionHandlerType.Fault);
    }

    public void BeginFinallyBlock()
    {
        _ExceptionHandlers.Peek().BeginHandler(ExceptionHandlerType.Finally);
    }

    public void EndExceptionBlock()
    {
        _ExceptionHandlers.Pop().End();
    }
}