type Props = {
    color: string;
    width?: number | string;
    height?: number | string;
};

export default function Spinner({ color, width = "100px", height = "100px" }: Props) {
    const innerClass = `w-[${width}] h-[${height}] border-[${color}] animate-spin rounded-full border-[20px] border-e-transparent`;

    return (
        <div
            className="absolute flex size-full justify-center items-center left-1/2 top-1/2 transform -translate-x-1/2 -translate-y-1/2">
            <div
                className={innerClass} />
        </div>
    );
}